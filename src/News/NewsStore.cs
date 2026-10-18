using System.Collections.Generic;
using System.IO;

namespace LevelLibrary;

/// <summary>更新公告的读写：正文在 mod 目录的 updates.txt，已读过的代号在 updates_read.txt。
/// 以后每次改动只往 updates.txt 添一行，不用动代码；文件不在就先写一份表头当说明。
/// 读的时机是大厅每次建屏，所以改完文件重开一次面板就能看到新的。</summary>
public static class NewsStore {
    public const string FileName = "updates.txt";
    public const string ReadFileName = "updates_read.txt";

    private static readonly List<NewsText.Entry> _all = new List<NewsText.Entry>();
    private static readonly HashSet<string> _read = new HashSet<string>();
    private static string _path, _readPath;

    /// <summary>收下的公告条数（含已经读过的）</summary>
    public static int Count { get { return _all.Count; } }

    /// <summary>第 i 条的代号（按文件里的先后，越靠后越新）</summary>
    public static string IdAt(int i) { return (i >= 0 && i < _all.Count) ? _all[i].Id : null; }

    /// <summary>第 i 条的正文</summary>
    public static string TextAt(int i) { return (i >= 0 && i < _all.Count) ? _all[i].Text : null; }

    /// <summary>公告文件路径</summary>
    public static string FilePath { get { return _path; } }

    /// <summary>还没读的条数</summary>
    public static int UnreadCount {
        get {
            int n = 0;
            for (int i = 0; i < _all.Count; i++) if (!_read.Contains(_all[i].Id)) n++;
            return n;
        }
    }

    /// <summary>没读的里面从新到旧数第 k 条，返回它在表里的下标（0 是最新的一条）。没有就返回 -1。</summary>
    public static int UnreadIndexAt(int k) {
        if (k < 0) return -1;
        int seen = 0;
        for (int i = _all.Count - 1; i >= 0; i--) {
            if (_read.Contains(_all[i].Id)) continue;
            if (seen == k) return i;
            seen++;
        }
        return -1;
    }

    /// <summary>读一次（先把内存里的清空，所以文件改短了也跟着变）</summary>
    public static void Reload() {
        _all.Clear();
        _read.Clear();
        try {
            if (_path == null) {
                string dir = Main.ModEntry.Path;
                _path = Path.Combine(dir, FileName);
                _readPath = Path.Combine(dir, ReadFileName);
            }
            if (File.Exists(_path)) {
                NewsText.Parse(File.ReadAllText(_path), _all);
            } else {
                File.WriteAllLines(_path, new string[] {
                    Lang.Tr("# 这里是更新公告：一行一条，写完保存，重启游戏就能在大厅左侧看到。"),
                    Lang.Tr("# 格式是「代号 | 正文」；代号用来记这条已经读过（建议用版本号或日期），不写代号就用正文本身。"),
                    Lang.Tr("# 例：v3.3.2 | 大厅左侧改成公告区，没读过的更新一条一张卡片，点掉就收起。"),
                });
                Main.ModEntry.Logger.Log("[LevelLib] 公告文件不在，已新建一份带说明的空 " + FileName);
            }
            if (File.Exists(_readPath)) {
                string[] lines = File.ReadAllLines(_readPath);
                for (int i = 0; i < lines.Length; i++) {
                    string s = lines[i].Trim();
                    if (s.Length > 0) _read.Add(s);
                }
            }
        } catch (System.Exception e) {
            PortalUI.ReportErrorQuiet("读取更新公告失败（" + FileName + " 这次没读进来，公告区先不摆卡片）", e);
        }
    }

    /// <summary>记一条已读并落盘。写不进去也照样算读过，免得同一张卡片这一局反复出现。</summary>
    public static bool MarkRead(string id) {
        if (string.IsNullOrEmpty(id)) return false;
        if (!_read.Add(id)) return true;
        if (_readPath == null) return false;
        try {
            string[] lines = new string[_read.Count];
            _read.CopyTo(lines);
            System.Array.Sort(lines, System.StringComparer.Ordinal);
            File.WriteAllLines(_readPath, lines);
            return true;
        } catch (System.Exception e) {
            PortalUI.ReportErrorQuiet("记录公告已读失败（" + ReadFileName + " 写不进去，重启后这条可能还会出现）", e);
            return false;
        }
    }
}
