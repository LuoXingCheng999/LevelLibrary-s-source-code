using System.Collections.Generic;

namespace LevelLibrary;

/// <summary>更新公告的文本解析。一行一条，代号与正文之间用一个竖线隔开。
/// 单独一个文件、而且只碰字符串，是为了能离线编译起来跑边界验证。
/// 行格式：代号 | 正文。不写代号就整行当正文，代号也取正文本身（正文改了就算新的一条）；
/// 想让同一条之后仍算已读，就把不变的代号写在前面（版本号或日期）。
/// 以井号开头的是注释，空行忽略。</summary>
public static class NewsText {
    /// <summary>一条更新公告：代号用来记已读，正文是卡片上显示的字</summary>
    public struct Entry {
        public string Id;
        public string Text;
    }

    private const char Sep = '|';

    /// <summary>把整份文本解析进 into（先清空）。返回收下的条数。</summary>
    public static int Parse(string text, List<Entry> into) {
        if (into == null) return 0;
        into.Clear();
        if (string.IsNullOrEmpty(text)) return 0;
        int n = 0;
        int i = 0;
        while (i < text.Length) {
            int j = text.IndexOf('\n', i);
            string line = (j < 0) ? text.Substring(i) : text.Substring(i, j - i);
            i = (j < 0) ? text.Length : j + 1;
            line = line.Trim();
            if (line.Length == 0) continue;
            if (line[0] == '#') continue;
            string id, body;
            int k = line.IndexOf(Sep);
            if (k < 0) {
                id = line;
                body = line;
            } else {
                id = line.Substring(0, k).Trim();
                body = line.Substring(k + 1).Trim();
                // 竖线只有半边（前面空、或后面空）就不当分隔符，整行都算正文
                if (id.Length == 0 || body.Length == 0) {
                    id = line;
                    body = line;
                }
            }
            Entry e;
            e.Id = id;
            e.Text = body;
            into.Add(e);
            n++;
        }
        return n;
    }
}
