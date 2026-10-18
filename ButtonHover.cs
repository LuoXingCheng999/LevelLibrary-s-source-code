using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LevelLibrary;

/// <summary>列表项悬停变色。行底色只有这一个写手：进出与按下事件只改标志位，
/// 颜色一律由 Apply 现算一次。行是对象池复用的（同一根行会被换去显示别的谱面），
/// 若把颜色写死在事件里，刷新那一份就会把悬停色盖掉、而指针没动不会再补一次进出事件，
/// 表现就是鼠标底下那一行忽亮忽灭。</summary>
public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
                           IPointerDownHandler, IPointerUpHandler {
    public Color Normal = Color.white;
    public bool IsSelected;
    /// <summary>这一行是「刚导入还没点开过」的谱面：常态用提示色亮一下。
    /// 它只是提示 —— 指针一压上来就让位给悬停色（见 Apply），玩家看过这一眼就够了。</summary>
    public bool IsNew;
    private Image _img;        // 只取自身那张 Image：挂在行根、图在子物体时，变色会落到行根的透明命中图上
    private bool _hovered;     // 指针是不是正压在这一行上
    private bool _pressed;     // 左键是不是正按在这一行上（按下反馈也归这里，行按钮关掉了自带的染色）

    private static readonly Color HoverColor = new Color(0.28f, 0.23f, 0.42f, 1f);
    private static readonly Color SelectedColor = new Color(1f, 0.55f, 0.75f, 1f);
    /// <summary>「刚导入」的提示色：比选中色淡一档并偏冷，与选中态一眼能分开
    ///（它表示"新来的"，不是"你现在选着它"）</summary>
    public static readonly Color NewColor = new Color(0.45f, 0.95f, 0.75f, 0.55f);
    private const float PressMul = 0.86f;    // 按下时整体压暗的比例（与大厅那几块同一个数）

    /// <summary>按当前状态刷底色；外部改完 IsSelected / IsNew / Normal 后调用。
    /// 优先级：选中 &gt; 悬停 &gt; 新导入提示 &gt; 常态，按下再压一层。
    /// 「提示色让位给悬停」是用户点名要的：鼠标压上去就换成悬停色，移开才回到提示色。</summary>
    public void Apply() {
        if (_img == null) _img = GetComponent<Image>();
        if (_img == null) return;
        Color c;
        if (IsSelected) c = SelectedColor;
        else if (_hovered) c = HoverColor;
        else if (IsNew) c = NewColor;
        else c = Normal;
        if (_pressed) c = new Color(c.r * PressMul, c.g * PressMul, c.b * PressMul, c.a);
        _img.color = c;
    }

    /// <summary>行被移出视口 / 换人时调：指针离开与松手的事件在「行被挪走」那一帧不一定会补发，
    /// 不清掉的话这行滚回来时会自带悬停色、或者一直是压暗的。</summary>
    public void ClearPointerState() {
        if (!_hovered && !_pressed) return;
        _hovered = false;
        _pressed = false;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) {
        _hovered = true;
        Apply();
    }

    public void OnPointerExit(PointerEventData eventData) {
        _hovered = false;
        _pressed = false;
        Apply();
    }

    public void OnPointerDown(PointerEventData eventData) {
        _pressed = true;
        Apply();
    }

    public void OnPointerUp(PointerEventData eventData) {
        _pressed = false;
        Apply();
    }
}
