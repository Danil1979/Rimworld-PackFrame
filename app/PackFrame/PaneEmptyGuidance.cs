namespace PackFrame;

internal sealed record PaneEmptyGuidance(string Title, string Body)
{
    internal static PaneEmptyGuidance Create(string? language, bool hasGame, bool hasCatalog,
        bool filtered, bool activePane, int sourceCount, int missingCount, bool hasInstance, bool isDamaged = false)
    {
        string T(string en, string hans, string hant, string ja) => OperationText.Choose(language, en, hans, hant, ja);
        if (!hasGame) return new(T("Choose your game folder", "选择游戏文件夹", "選擇遊戲資料夾", "ゲームフォルダーを選択"),
            T("Choose a RimWorld folder above to load your library.", "在上方选择 RimWorld 文件夹以加载模组库。", "在上方選擇 RimWorld 資料夾以載入模組庫。", "上部で RimWorld フォルダーを選択してライブラリを読み込みます。"));
        if (activePane && !hasInstance) return new(T("Choose an instance", "选择实例", "選擇實例", "インスタンスを選択"),
            T("Use the instance selector above this list.", "使用此列表上方的实例选择框。", "使用此清單上方的執行個體選擇框。", "このリストの上でインスタンスを選択してください。"));
        if (activePane && isDamaged) return new(T("Instance unreadable", "无法读取实例", "無法讀取實例", "インスタンスを読み取れません"),
            T("This list is read-only. Repair its configuration files, then Refresh. Hover over the instance selector for the failing path.",
                "此列表为只读。修复配置文件后点击“刷新”。将鼠标悬停在实例选择框上可查看出错路径。",
                "此清單為唯讀。修復設定檔後點選「重新整理」。將滑鼠停在實例選擇框上可查看錯誤路徑。",
                "このリストは読み取り専用です。設定ファイルを修復して「更新」してください。インスタンス選択欄にマウスを置くとエラーのパスが表示されます。"));
        if (filtered && sourceCount > 0) return new(T("No matching mods", "没有匹配的模组", "沒有符合的模組", "一致する MOD がありません"),
            T("Press Esc in this search field, or use its × to clear search and filters.", "在搜索框中按 Esc，或点击末尾的 × 清除搜索和筛选。", "在搜尋框中按 Esc，或點選末尾的 × 清除搜尋和篩選。", "検索欄で Esc を押すか、末尾の × で検索とフィルターを解除します。"));
        if (activePane && missingCount > 0) return new(T($"{missingCount} mods unavailable", $"{missingCount} 个模组不可用", $"{missingCount} 個模組無法使用", $"{missingCount} 個の MOD が利用できません"),
            T("Your load order is kept. Open Missing below this list to review unavailable mods and download status.", "加载顺序已保留。点击此列表下方的“缺失”查看不可用模组和下载状态。", "載入順序已保留。點選此清單下方的「缺失」查看無法使用的模組與下載狀態。", "読み込み順は保持されています。このリストの下の「欠落」で、利用できない MOD とダウンロード状況を確認してください。"));
        if (!hasCatalog) return new(T("No installed mods found", "未找到已安装模组", "找不到已安裝模組", "インストール済み MOD が見つかりません"),
            T("Check the game folder above. After installing or restoring mod files, choose Refresh.", "请检查上方的游戏文件夹。安装或恢复模组文件后，点击“刷新”。", "請檢查上方的遊戲資料夾。安裝或還原模組檔案後，點選「重新整理」。", "上部のゲームフォルダーを確認し、MOD ファイルのインストールや復元後に「更新」を選んでください。"));
        return new(T("No mods here yet", "此处暂无模组", "此處尚無模組", "まだ MOD がありません"), activePane
            ? T("Select mods in Inactive, then choose Enable above this list.", "在未启用列表中选择模组，然后点击此列表上方的“启用”按钮。", "在未啟用清單中選擇模組，然後點選此清單上方的「啟用」按鈕。", "無効リストで MOD を選び、このリストの上の「有効化」で追加します。")
            : T("Every available mod is already active in the selected instance.", "所选实例已启用所有可用模组。", "所選實例已啟用所有可用模組。", "選択中のインスタンスでは、利用できる MOD がすべて有効です。"));
    }
}
