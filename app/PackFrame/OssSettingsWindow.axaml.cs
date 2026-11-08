using System;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Interactivity;

namespace PackFrame;

public partial class OssSettingsWindow : Window
{
    private readonly AppConfig _config;
    private readonly string _language;
    private bool _validationRequested;

    public OssSettingsWindow()
    {
        InitializeComponent();
        _config = new AppConfig();
        _language = "zh-Hans";
    }

    public OssSettingsWindow(AppConfig config, string? language)
        : this()
    {
        _config = config;
        _language = string.IsNullOrWhiteSpace(language) ? "zh-Hans" : language;
        Configure();
    }

    private void Configure()
    {
        Title = OperationText.Choose(_language, "OSS settings", "OSS 配置", "OSS 設定", "OSS 設定");
        OssTitle.Text = OperationText.Choose(_language, "Aliyun OSS", "阿里云 OSS", "阿里雲 OSS", "Aliyun OSS");
        OssHint.Text = OperationText.Choose(
            _language,
            "Used for local mod archives. Metadata still goes to Gitee/GitHub. Save checks the format only; connection and permissions are checked when uploading.",
            "用于本地模组压缩包，元数据仍上传到 Gitee/GitHub。保存只检查格式；连接和权限会在上传时检查。",
            "用於本機模組壓縮檔，中繼資料仍上傳到 Gitee/GitHub。儲存只檢查格式；連線與權限會在上傳時檢查。",
            "ローカル MOD のアーカイブ用です。メタデータは Gitee/GitHub に保存します。保存時は形式のみ確認し、接続と権限はアップロード時に確認します。"
        );
        OssEnabledCheckBox.Content = OperationText.Choose(
            _language,
            "Enable OSS upload",
            "启用 OSS 上传",
            "啟用 OSS 上傳",
            "OSS アップロードを有効にする"
        );
        OssEnabledCheckBox.IsChecked = _config.OssEnabled;
        EndpointLabel.Text = OperationText.Choose(
            _language,
            "Endpoint (hostname or HTTPS URL)",
            "Endpoint（主机名或 HTTPS URL）",
            "Endpoint（主機名稱或 HTTPS URL）",
            "Endpoint（ホスト名または HTTPS URL）"
        );
        BucketLabel.Text = OperationText.Choose(_language, "Bucket", "Bucket", "Bucket", "Bucket");
        PrefixLabel.Text = OperationText.Choose(
            _language,
            "Object prefix (optional)",
            "对象前缀（可选）",
            "物件前綴（可選）",
            "オブジェクト接頭辞（任意）"
        );
        AccessKeyIdLabel.Text = OperationText.Choose(
            _language,
            "AccessKey ID",
            "AccessKey ID",
            "AccessKey ID",
            "AccessKey ID"
        );
        AccessKeySecretLabel.Text = OperationText.Choose(
            _language,
            "AccessKey Secret",
            "AccessKey Secret",
            "AccessKey Secret",
            "AccessKey Secret"
        );
        NoteText.Text = OperationText.Choose(
            _language,
            "Keys are saved unencrypted in local packframe.json. Anyone who can read it can use them. Do not share or commit this file.\nUse a RAM key limited to PutObject for the intended bucket/prefix. Shared archive links must be readable without this key.\nDisabling upload keeps saved keys. To remove them, disable upload, clear both key fields, then Save. This does not revoke the key at OSS.",
            "密钥以未加密的明文保存在本机 packframe.json 中。能读取此文件的人即可使用密钥。请勿分享或提交此文件。\n建议使用仅对目标 Bucket/前缀具有 PutObject 权限的 RAM 密钥。分享的压缩包链接须能在不提供此密钥的情况下读取。\n关闭上传不会删除密钥。要移除已保存的密钥，请关闭上传、清空两个密钥字段，然后保存。这不会在 OSS 中吊销密钥。",
            "金鑰以未加密的明文儲存在本機 packframe.json 中。能讀取此檔案的人即可使用金鑰。請勿分享或提交此檔案。\n建議使用僅對目標 Bucket/前綴具有 PutObject 權限的 RAM 金鑰。分享的壓縮檔連結須能在不提供此金鑰的情況下讀取。\n關閉上傳不會刪除金鑰。若要移除已儲存的金鑰，請關閉上傳、清空兩個金鑰欄位，再儲存。這不會在 OSS 中撤銷金鑰。",
            "キーはローカルの packframe.json に暗号化せず保存されます。このファイルを読める人はキーを使用できます。共有やコミットはしないでください。\n対象のバケットと接頭辞への PutObject 権限に限定した RAM キーを使用してください。共有するアーカイブ URL は、このキーなしで読み取れる必要があります。\nアップロードを無効にしてもキーは残ります。削除するには無効にし、両方のキー欄を空にして保存してください。OSS 側でのキーの失効は行われません。"
        );
        CancelButton.Content = OperationText.Choose(_language, "Cancel", "取消", "取消", "キャンセル");
        SaveButton.Content = OperationText.Choose(_language, "Save", "保存", "儲存", "保存");
        CancelButton.IsCancel = true;
        SaveButton.IsDefault = true;
        EndpointBox.Text = _config.OssEndpoint ?? string.Empty;
        EndpointBox.PlaceholderText = "oss-cn-hangzhou.aliyuncs.com";
        BucketBox.Text = _config.OssBucket ?? string.Empty;
        PrefixBox.Text = _config.OssPrefix ?? string.Empty;
        AccessKeyIdBox.Text = _config.OssAccessKeyId ?? string.Empty;
        AccessKeySecretBox.Text = _config.OssAccessKeySecret ?? string.Empty;
        foreach (var (input, label) in new[]
        {
            (EndpointBox, EndpointLabel), (BucketBox, BucketLabel), (PrefixBox, PrefixLabel),
            (AccessKeyIdBox, AccessKeyIdLabel), (AccessKeySecretBox, AccessKeySecretLabel)
        })
        {
            AutomationProperties.SetName(input, label.Text);
            AutomationProperties.SetLabeledBy(input, label);
            input.TextChanged += (_, _) => OnDraftChanged();
        }
        OssEnabledCheckBox.IsCheckedChanged += (_, _) => OnDraftChanged();
        UpdateValidation();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _validationRequested = true;
        var validation = UpdateValidation();
        if (!validation.IsValid)
        {
            foreach (var (field, input, _, _) in ValidationFields())
                if (validation.Errors.ContainsKey(field))
                {
                    input.Focus();
                    input.BringIntoView();
                    break;
                }
            return;
        }

        if (!validation.Draft.TrySave(_config))
        {
            StatusText.Text = OperationText.Choose(
                _language,
                "Settings could not be saved. Check that the PackFrame folder is writable and the disk has free space, then try Save again. Your previous settings are unchanged.",
                "无法保存设置。请检查 PackFrame 文件夹的写入权限和磁盘剩余空间，然后重试保存。原有设置未改变。",
                "無法儲存設定。請檢查 PackFrame 資料夾的寫入權限和磁碟剩餘空間，然後重試儲存。原有設定未變更。",
                "設定を保存できません。PackFrame フォルダーの書き込み権限とディスクの空き容量を確認し、もう一度保存してください。以前の設定は変更されていません。"
            );
            StatusText.BringIntoView();
            return;
        }
        Close(true);
    }

    private OssSettingsDraft ReadDraft() => new(OssEnabledCheckBox.IsChecked == true,
        EndpointBox.Text, BucketBox.Text, PrefixBox.Text, AccessKeyIdBox.Text, AccessKeySecretBox.Text);

    private (OssSettingsField Field, TextBox Input, Border Border, TextBlock Error)[] ValidationFields() => new[]
    {
        (OssSettingsField.Endpoint, EndpointBox, EndpointField, EndpointError),
        (OssSettingsField.Bucket, BucketBox, BucketField, BucketError),
        (OssSettingsField.AccessKeyId, AccessKeyIdBox, AccessKeyIdField, AccessKeyIdError),
        (OssSettingsField.AccessKeySecret, AccessKeySecretBox, AccessKeySecretField, AccessKeySecretError)
    };

    private void OnDraftChanged()
    {
        StatusText.Text = string.Empty;
        UpdateValidation();
    }

    private OssSettingsValidation UpdateValidation()
    {
        var draft = ReadDraft();
        var validation = draft.Validate();
        foreach (var (field, input, border, errorText) in ValidationFields())
        {
            var invalid = _validationRequested && validation.Errors.ContainsKey(field);
            errorText.Text = invalid ? ValidationMessage(validation.Errors[field]) : string.Empty;
            errorText.IsVisible = invalid;
            if (invalid && !border.Classes.Contains("invalid")) border.Classes.Add("invalid");
            if (!invalid) border.Classes.Remove("invalid");
            AutomationProperties.SetHelpText(input, errorText.Text);
        }
        var host = draft.UploadHost();
        DestinationText.IsVisible = host is not null;
        DestinationText.Text = host is null ? string.Empty : OperationText.Choose(_language,
            "Upload host: ", "上传主机：", "上傳主機：", "アップロード先ホスト: ") + host;
        if (_validationRequested)
            StatusText.Text = validation.IsValid ? string.Empty : OperationText.Choose(_language,
                "Correct the highlighted fields, then Save.", "请修正标记的字段，再保存。",
                "請修正標記的欄位，再儲存。", "示された入力欄を修正してから保存してください。");
        return validation;
    }

    private string ValidationMessage(OssSettingsError error) => error switch
    {
        OssSettingsError.Required => OperationText.Choose(_language,
            "This field is required while OSS upload is enabled.", "启用 OSS 上传时必须填写此字段。",
            "啟用 OSS 上傳時必須填寫此欄位。", "OSS アップロードが有効な場合、この項目は必須です。"),
        OssSettingsError.EndpointScheme => OperationText.Choose(_language,
            "Uploads use HTTPS. Enter a hostname or an https:// URL.", "上传使用 HTTPS。请输入主机名或 https:// URL。",
            "上傳使用 HTTPS。請輸入主機名稱或 https:// URL。", "アップロードには HTTPS を使用します。ホスト名または https:// URL を入力してください。"),
        OssSettingsError.EndpointComponents => OperationText.Choose(_language,
            "Enter only the service hostname and optional port. Do not include a path, query, fragment, or login details.",
            "只需填写服务主机名及可选端口，不要包含路径、查询参数、片段或登录信息。",
            "只需填寫服務主機名稱及選用連接埠，不要包含路徑、查詢參數、片段或登入資訊。",
            "サービスのホスト名と任意のポートのみ入力してください。パス、クエリ、フラグメント、ログイン情報は含めないでください。"),
        OssSettingsError.EndpointHost => OperationText.Choose(_language,
            "Enter a valid hostname, not an IP address. PackFrame adds the bucket name before this host.",
            "请输入有效主机名，而非 IP 地址。PackFrame 会在此主机名前添加 Bucket 名称。",
            "請輸入有效主機名稱，而非 IP 位址。PackFrame 會在此前面加上 Bucket 名稱。",
            "IP アドレスではなく有効なホスト名を入力してください。PackFrame はその前にバケット名を付けます。"),
        OssSettingsError.BucketFormat => OperationText.Choose(_language,
            "Use 3–63 lowercase letters, digits, or hyphens. Start and end with a letter or digit.",
            "使用 3–63 个小写字母、数字或连字符，首尾须为字母或数字。",
            "使用 3–63 個小寫字母、數字或連字號，開頭與結尾須為字母或數字。",
            "3～63 文字の小文字英字、数字、ハイフンを使用し、先頭と末尾は英字または数字にしてください。"),
        OssSettingsError.AccessKeyIdWhitespace => OperationText.Choose(_language,
            "AccessKey ID cannot contain spaces, line breaks, or control characters.",
            "AccessKey ID 不能包含空格、换行或控制字符。", "AccessKey ID 不可包含空格、換行或控制字元。",
            "AccessKey ID に空白、改行、制御文字を含めることはできません。"),
        _ => OperationText.Choose(_language,
            "Enter a valid hostname with an optional port, for example oss.example.com:9443.",
            "请输入有效主机名及可选端口，例如 oss.example.com:9443。",
            "請輸入有效主機名稱及選用連接埠，例如 oss.example.com:9443。",
            "有効なホスト名と任意のポートを入力してください。例: oss.example.com:9443。")
    };

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
