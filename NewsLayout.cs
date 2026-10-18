namespace LevelLibrary;

/// <summary>公告区卡片的排布：把整片区域切成小格，卡片一行一行铺下去。
/// 一行的行高由这一行卡片的宽高比合计定（攒够目标高就换行），每张卡的宽度按自己的宽高比摊，
/// 于是行高在目标上下浮动、卡片宽窄各异，看着像随手摆的一堆，而不是一张列表。
/// 卡片少了地就多，剩下的行会整体加高（填充倍数），所以点掉一张其余的会跟着长大。
/// 两条硬约束：谁也不许越出区域，谁也不许压着谁。
/// 悬停那张占满最上面一整条、按需要的高度显示整段正文，别人被挤到下面。
/// 悬停的认定只看光标自己挪了多少，跟放大引起的重排解耦：拿重排之后的位置重新认人，
/// 认定与重排就会互相追着改，卡片一张一张抖起来。单独一个文件也是为了能离线跑数值验证。</summary>
public static class NewsLayout {
    /// <summary>卡片区在左区遮罩里的内缩</summary>
    public const float PadX = 16f, PadTop = 16f, PadBottom = 16f;

    /// <summary>格子边长。所有位置与大小都吸附到它，卡片之间因此永远对得齐</summary>
    public const float Cell = 24f;

    /// <summary>每块四周往里收这么多，留出卡与卡的缝（实际缝是这个数的两倍）</summary>
    public const float Inset = 8f;

    /// <summary>最小块（格）：再小就只剩一条，字读不出来了</summary>
    public const int MinCW = 5, MinCH = 3;

    /// <summary>一行的目标高（格）与最多几张：攒够这个高度就换行，行高因此在这一档上下浮动</summary>
    public const int TargetRowH = 7, MaxCardsPerRow = 6;

    /// <summary>单行最高几格：一张卡的一行不至于拉成一条横幅</summary>
    public const int MaxRowH = 14;

    /// <summary>整片区域最多把这些行整体加高多少倍（卡片少的时候靠它长大），
    /// 以及最少压到多矮（悬停那张挤过来时别的行要跟着缩）</summary>
    public const float MaxFill = 2.2f, MinFill = 0.25f;

    /// <summary>悬停那张至少比平时的行高这么多倍</summary>
    public const float HoverGrow = 1.35f;

    /// <summary>每张卡的宽高比范围：同分量也要形状各异，看着才像随手摆的</summary>
    public const float AspectMin = 0.85f, AspectMax = 2.35f;

    /// <summary>一批最多摆几张（再多就等前面的点掉再顶上）</summary>
    public const int MaxCards = 64;

    /// <summary>一块地：格坐标（自己找的位置）与算好的像素矩形（相对区域左上角）</summary>
    public struct Block {
        public int CX, CY, CW, CH;
        public float X, Y, W, H;
    }

    /// <summary>一行能切出几格</summary>
    public static int Cols(float regionW) {
        int c = (int)((regionW - PadX * 2f) / Cell);
        if (c < MinCW) c = MinCW;
        return c;
    }

    /// <summary>一列能切出几格</summary>
    public static int Rows(float regionH) {
        int r = (int)((regionH - PadTop - PadBottom) / Cell);
        if (r < MinCH) r = MinCH;
        return r;
    }

    /// <summary>这一区里最多摆得下几张：按目标行高估行数，按目标行高与平均宽高比估每行张数</summary>
    public static int FitCount(float regionW, float regionH) {
        int cols = Cols(regionW), rows = Rows(regionH);
        int rowCount = rows / TargetRowH;
        if (rowCount < 1) rowCount = 1;
        int cards = rowCount * (int)(cols / (TargetRowH * 1.6f) + 1f);
        if (cards < 1) cards = 1;
        if (cards > MaxCards) cards = MaxCards;
        return cards;
    }

    /// <summary>一整段正文铺满一行大概要占多高：按字宽估行数，宁可多给一点，免得悬停了还看不全</summary>
    public static float TextNeededH(int chars, float innerW, float bodyFont) {
        if (chars < 1) chars = 1;
        if (bodyFont < 1f) bodyFont = 1f;
        float perLine = innerW / (bodyFont * 1.05f);
        if (perLine < 6f) perLine = 6f;
        int lines = (int)(chars / perLine) + 1;
        return lines * bodyFont * 1.35f;
    }

    /// <summary>把这一批卡片铺下去。weights 是每张卡的分量（越大越宽），aspects 是宽高比；
    /// hovered 那张占住最上面一整条，高度取"够写下整段正文"与"比平时那行高出一截"里更高的那个。
    /// 返回真正摆下的张数：高度用光之后剩下的卡不写格子表，交给这一批之外的下一批。</summary>
    public static int Pack(float regionW, float regionH, int count, int hovered,
                           float[] weights, float[] aspects, float hoverNeedPx, Block[] blocks) {
        if (count <= 0 || weights == null || aspects == null || blocks == null) return 0;
        if (weights.Length < count || aspects.Length < count || blocks.Length < count) return 0;
        int cols = Cols(regionW), rows = Rows(regionH);
        bool hasHover = hovered >= 0 && hovered < count;
        float rowTarget = RowTarget(count, hovered, cols, aspects, weights);

        // 第一遍：先走一遍，看这些卡按目标行高摊下来一共多高、要几行，好算出把整片区域铺满的倍数
        int rowCount;
        float sumTarget, maxRowTarget;
        WalkRows(count, hovered, cols, aspects, weights, rowTarget, out rowCount, out sumTarget, out maxRowTarget);
        float fillAll = (sumTarget > 0.01f) ? (rows / sumTarget) : 1f;
        if (fillAll < 1f) fillAll = 1f;
        if (fillAll > MaxFill) fillAll = MaxFill;

        // 悬停那张：要写得下整段正文（可见高是"格数乘格减两边内缩"，所以换算时要把内缩加回去），
        // 也要明显高过平时那一行。它最多占到"其他的卡仍按最小高度排得下"，免得一悬停别的卡被挤走。
        int hoverCH = 0;
        if (hasHover) {
            int cap = HoverCap(regionW, regionH, count, hovered, weights, aspects);
            float want = fillAll * maxRowTarget * HoverGrow;
            float textH = (hoverNeedPx + Inset * 2f) / Cell;
            if (textH > want) want = textH;
            hoverCH = Clamp((int)(want + 0.99f), MinCH, cap);
        }

        // 它占掉之后，剩下的行按剩下的高度重新摊：所以悬停一涨，别的行就跟着缩，而不是被挤掉。
        // 要挤进这块高度，所以二分找一个尽量大的倍数：能大就尽量大，装不下就一起缩。
        int avail = rows - hoverCH;
        float fill;
        if (hoverCH > 0) {
            // 悬停占掉一条之后剩下的地：别的行必须全挤进来，所以上限从 MaxFill 开始二分
            fill = FitFill(count, hovered, cols, aspects, weights, rowTarget, avail, MaxFill);
        } else {
            // 没有悬停：按目标行高摊一次，摊不下（卡多）再往下压一点，不把最后几张丢给下一批
            float natural = (sumTarget > 0.01f) ? (rows / sumTarget) : 1f;
            if (natural < 1f) natural = 1f;
            if (natural > MaxFill) natural = MaxFill;
            fill = (RowsHeight(count, hovered, cols, aspects, weights, rowTarget, natural) <= rows)
                 ? natural
                 : FitFill(count, hovered, cols, aspects, weights, rowTarget, rows, natural);
        }

        // 第二遍：真正摆。悬停那张先占最上面一整条，其余一行行铺下去
        int y = 0, placed = 0;
        if (hoverCH > 0) {
            Write(blocks, hovered, 0, 0, cols, hoverCH);
            placed++;
            y = hoverCH;
        }
        int i = 0;
        while (i < count && y + MinCH <= rows) {
            float sumA, h0;
            int to = NextRow(i, count, hovered, cols, aspects, weights, rowTarget, out sumA, out h0);
            if (sumA <= 0f) break;
            int h = Clamp((int)(fill * h0 + 0.5f), MinCH, MaxRowH);
            if (h > rows - y) h = rows - y;
            if (h < MinCH) break;

            // 摊宽度：按宽高比摊，但要给这一行剩下的每一张都留住最小宽度，
            // 最后一张吃掉余量：整行于是永远正好铺满，谁也不会被挤成一条缝。
            int x = 0, left = 0;
            for (int j = i; j < to; j++) if (j != hovered) left++;
            for (int j = i; j < to; j++) {
                if (j == hovered) continue;
                int w;
                if (left <= 1) {
                    w = cols - x;
                } else {
                    w = (int)(AspectOf(aspects[j], weights[j]) / sumA * cols + 0.5f);
                    int room = cols - x - (left - 1) * MinCW;
                    if (room < MinCW) room = MinCW;
                    if (w > room) w = room;
                    if (w < MinCW) w = MinCW;
                }
                if (w < MinCW) break;
                Write(blocks, j, x, y, w, h);
                placed++;
                x += w;
                left--;
                if (left <= 0) break;
            }
            y += h;
            i = to;
        }
        return placed;
    }

    /// <summary>把格坐标换成像素矩形（相对区域左上角），四周按 Inset 收进去</summary>
    public static void ToRect(Block b, out float x, out float y, out float w, out float h) {
        x = PadX + b.CX * Cell + Inset;
        y = PadTop + b.CY * Cell + Inset;
        w = b.CW * Cell - Inset * 2f;
        h = b.CH * Cell - Inset * 2f;
    }

    /// <summary>把像素长换算成几格，给外部算"悬停那张要写多少字"用</summary>
    public static int CellOf(float px) {
        int c = (int)(px / Cell);
        return c < 1 ? 1 : c;
    }

    /// <summary>悬停那张最高能占几格：先把别的卡按最小高度各留一行，剩下的全给它。
    /// 外面的调用方拿它算"这一段的正文最多能显示多少"，也免得悬停把同批的卡整个挤走。</summary>
    public static int HoverCap(float regionW, float regionH, int count, int hovered,
                               float[] weights, float[] aspects) {
        int rows = Rows(regionH);
        if (count <= 1 || hovered < 0 || hovered >= count) return rows;
        if (weights == null || aspects == null) return rows;
        if (weights.Length < count || aspects.Length < count) return rows;
        int cols = Cols(regionW);
        int rowCount;
        float sumTarget, maxRowTarget;
        WalkRows(count, hovered, cols, aspects, weights, RowTarget(count, hovered, cols, aspects, weights),
                 out rowCount, out sumTarget, out maxRowTarget);
        int cap = rows - rowCount * MinCH;
        if (cap < MinCH) cap = MinCH;
        if (cap > rows) cap = rows;
        return cap;
    }

    /// <summary>公告区当前认住的那一张，以及认定那一刻光标在哪</summary>
    public struct HotState {
        public int Index;
        public float X, Y;
    }

    /// <summary>光标落在这套格子的哪一块上（落在缝里或区外就是 -1）。没摆下的块宽高为零，认不到</summary>
    public static int HotAt(float mx, float my, int count, Block[] blocks) {
        if (blocks == null) return -1;
        if (count > blocks.Length) count = blocks.Length;
        for (int i = 0; i < count; i++) {
            Block b = blocks[i];
            if (b.W <= 0f || b.H <= 0f) continue;
            if (mx >= b.X && mx <= b.X + b.W && my >= b.Y && my <= b.Y + b.H) return i;
        }
        return -1;
    }

    /// <summary>光标从这个点挪出去了没有：比 slack 远才算挪出去</summary>
    public static bool HotStay(float mx, float my, float hx, float hy, float slack) {
        float dx = mx - hx, dy = my - hy;
        return dx * dx + dy * dy <= slack * slack;
    }

    /// <summary>这一帧认哪一张。光标没挪出一个格子就还认上一张：悬停那张会飞到最上面、别的卡跟着重排，
    /// 光标底下因此会换人，拿换过之后的位置重新认人就会一边认一边重排、卡片抖个不停。
    /// 所以只认光标自己挪了多少 —— 动了才按当前这套格子重新认人，不动就一直认着同一张。</summary>
    public static int HotTrack(float mx, float my, int count, Block[] blocks, float slack, ref HotState st) {
        if (count <= 0) {
            st.Index = -1;
            st.X = mx;
            st.Y = my;
            return -1;
        }
        if (st.Index >= 0 && st.Index < count && HotStay(mx, my, st.X, st.Y, slack)) return st.Index;
        int hit = HotAt(mx, my, count, blocks);
        st.Index = hit;
        st.X = mx;
        st.Y = my;
        return hit;
    }

    /// <summary>一行攒到多少宽高比合计就换行：先按目标行高估一下要几行，再把总宽高比平摊给这几行。
    /// 平摊是为了别让最后只剩一张卡、被拉成横贯整区的一条横幅</summary>
    private static float RowTarget(int count, int hovered, int cols, float[] aspects, float[] weights) {
        float perRow = cols / (float)TargetRowH;
        float sumAll = 0f;
        int n = 0;
        for (int i = 0; i < count; i++) {
            if (i == hovered) continue;
            sumAll += AspectOf(aspects[i], weights[i]);
            n++;
        }
        if (n <= 0) return perRow;
        int rowCount = (int)(sumAll / perRow + 0.5f);
        if (rowCount < 1) rowCount = 1;
        int need = (n + MaxCardsPerRow - 1) / MaxCardsPerRow;
        if (rowCount < need) rowCount = need;
        float t = sumAll / rowCount;
        float lo = cols / (float)MaxRowH;
        if (t < lo) t = lo;
        float hi = cols / (float)MinCH;
        if (t > hi) t = hi;
        return t;
    }

    /// <summary>把这一批卡按目标行高走一遍：跳过悬停那张，数出一共几行、行高合计、最高的一行</summary>
    private static void WalkRows(int count, int hovered, int cols, float[] aspects, float[] weights,
                                 float rowTarget, out int rowCount, out float sumTarget, out float maxRowTarget) {
        int i = 0;
        rowCount = 0;
        sumTarget = 0f;
        maxRowTarget = 0f;
        while (i < count) {
            float sumA, h0;
            int to = NextRow(i, count, hovered, cols, aspects, weights, rowTarget, out sumA, out h0);
            if (sumA <= 0f) break;
            sumTarget += h0;
            rowCount++;
            if (h0 > maxRowTarget) maxRowTarget = h0;
            i = to;
        }
    }

    /// <summary>二分找一个尽量大的填充倍数，使这些行正好挤进 avail 这么高。
    /// 从下限起就已经装不下时会返回下限：那一批摆不完的卡留给下一批，不硬塞。</summary>
    private static float FitFill(int count, int hovered, int cols, float[] aspects, float[] weights,
                                 float rowTarget, int avail, float hi) {
        if (hi > MaxFill) hi = MaxFill;
        float lo = MinFill;
        if (hi <= lo) return lo;
        if (RowsHeight(count, hovered, cols, aspects, weights, rowTarget, hi) <= avail) return hi;
        for (int guard = 0; guard < 20; guard++) {
            float mid = (lo + hi) * 0.5f;
            if (RowsHeight(count, hovered, cols, aspects, weights, rowTarget, mid) <= avail) lo = mid;
            else hi = mid;
        }
        return lo;
    }

    /// <summary>按某个倍数摊下来，这些行一共要占多高（只量不写）：用来把倍数压到刚好装得下</summary>
    private static int RowsHeight(int count, int hovered, int cols,
                                  float[] aspects, float[] weights, float rowTarget, float fill) {
        int i = 0, total = 0;
        while (i < count) {
            float sumA, h0;
            i = NextRow(i, count, hovered, cols, aspects, weights, rowTarget, out sumA, out h0);
            if (sumA <= 0f) break;
            total += Clamp((int)(fill * h0 + 0.5f), MinCH, MaxRowH);
        }
        return total;
    }

    /// <summary>从 from 开始攒一行（跳过悬停那张）：返回这一行之后的下标，
    /// 并给出这一行的宽高比合计与按目标高算的行高</summary>
    private static int NextRow(int from, int count, int hovered, int cols, float[] aspects, float[] weights,
                               float rowTarget, out float sumA, out float h0) {
        sumA = 0f;
        int k = from, used = 0;
        while (k < count && used < MaxCardsPerRow) {
            if (k != hovered) {
                sumA += AspectOf(aspects[k], weights[k]);
                used++;
                if (sumA >= rowTarget) {
                    k++;
                    break;
                }
            }
            k++;
        }
        h0 = (sumA > 0f) ? (cols / sumA) : TargetRowH;
        if (h0 > TargetRowH) h0 = TargetRowH;
        if (h0 < MinCH) h0 = MinCH;
        return k;
    }

    /// <summary>一张卡摊多宽：宽高比为主，分量稍微再加一点，分量重的自然宽一些</summary>
    private static float AspectOf(float aspect, float weight) {
        float a = Safe(aspect);
        float w = Safe(weight);
        if (w > 3f) w = 3f;
        return a * (0.7f + 0.3f * w);
    }

    private static float Safe(float v) {
        if (float.IsNaN(v)) return 1f;
        if (v < 0.01f) return 0.01f;
        return v;
    }

    private static int Clamp(int v, int lo, int hi) {
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }

    private static void Write(Block[] blocks, int i, int cx, int cy, int cw, int ch) {
        Block b = default(Block);
        b.CX = cx;
        b.CY = cy;
        b.CW = cw;
        b.CH = ch;
        float x, y, w, h;
        ToRect(b, out x, out y, out w, out h);
        b.X = x;
        b.Y = y;
        b.W = w;
        b.H = h;
        blocks[i] = b;
    }
}
