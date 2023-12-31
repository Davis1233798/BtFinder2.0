﻿using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

public class MainForm : Form
{
    private static HttpClient httpClient = CreateHttpClient();
    private const string BaseUrl = "https://tellme.pw/btsow";
    private Timer clipboardCheckTimer = new Timer();
    private string lastClipboardText = string.Empty;
    private NotifyIcon notifyIcon;
    public MainForm()
    {
        this.WindowState = FormWindowState.Minimized;
        this.ShowInTaskbar = false;

        SetupNotifyIcon();

        clipboardCheckTimer.Interval = 1000;
        clipboardCheckTimer.Tick += ClipboardCheckTimer_Tick;
        clipboardCheckTimer.Start();
    }
    private void SetupNotifyIcon()
    {
        notifyIcon = new NotifyIcon();
        notifyIcon.Text = "BTFinder";  // 這將是當鼠標懸停在圖標上時顯示的文本。
        notifyIcon.Icon = this.Icon;  // 您可以設置為任何其他圖標。
        notifyIcon.Visible = true;

        // 添加一個上下文菜單，提供退出選項。
        ContextMenu contextMenu = new ContextMenu();
        MenuItem exitMenuItem = new MenuItem("Exit", ExitMenuItem_Click);
        contextMenu.MenuItems.Add(exitMenuItem);
        notifyIcon.ContextMenu = contextMenu;

        // 處理雙擊事件。
        notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
    }

    private void NotifyIcon_DoubleClick(object sender, EventArgs e)
    {
        this.WindowState = FormWindowState.Normal;
        this.ShowInTaskbar = true;
    }

    private void ExitMenuItem_Click(object sender, EventArgs e)
    {
        this.Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        notifyIcon.Dispose();  // 確保清除 NotifyIcon。
    }

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        return httpClient;
    }

    private void ClipboardCheckTimer_Tick(object sender, EventArgs e)
    {
        var clipboardText = Clipboard.GetText();
        if (clipboardText != lastClipboardText && clipboardText.Contains("-") && clipboardText.Count(c => c == '-') <= 2)
        {
            lastClipboardText = clipboardText;
            ProcessClipboardContent(clipboardText);
        }
    }

    private async void ProcessClipboardContent(string clipboardText)
    {
        try
        {
            var html = await httpClient.GetStringAsync(BaseUrl);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var node = doc.DocumentNode.SelectSingleNode("//div[@class='col-lg-2 col-md-3 col-sm-4 hidden-xs']/a");
            if (node == null) return;

            var href = node.GetAttributeValue("href", string.Empty);
            var targetUrl = $"{href}/search/{clipboardText}";

            var targetHtml = await httpClient.GetStringAsync(targetUrl);
            doc.LoadHtml(targetHtml);

            var rows = doc.DocumentNode.SelectNodes("//div[@class='data-list']/div[@class='row']");
            if (rows == null || !rows.Any()) return;

            string maxHref = string.Empty;
            double maxSize = 0;

            foreach (var row in rows)
            {
                var sizeNode = row.SelectSingleNode(".//div[@class='col-sm-2 col-lg-1 hidden-xs text-right size']");
                var sizeText = sizeNode?.InnerText.Trim() ?? string.Empty;
                if (fileNode != null)
                {
                    var fileName = fileNode.InnerText.Trim();
                    if (fileName.Contains(".zip") || fileName.Contains(".rar") || fileName.Contains(".iso"))
                    {
                        continue;
                    }
                }

                if (TryParseSize(sizeText, out double currentSize) && currentSize > maxSize)
                {
                    if (currentSize > 10240) continue;
                    maxSize = currentSize;
                    var hrefNode = row.SelectSingleNode(".//a");
                    maxHref = hrefNode.GetAttributeValue("href", string.Empty);
                }
            }

            if (!string.IsNullOrWhiteSpace(maxHref))
            {
                var lastSegment = "magnet:?xt=urn:btih:" + maxHref.Substring(maxHref.LastIndexOf('/') + 1);
                Clipboard.SetText(lastSegment);
            }
        }
        catch
        {
            // 如果有需要，您可以在此處添加日誌或錯誤處理
        }
    }

    private bool TryParseSize(string sizeText, out double size)
    {
        size = 0;
        var match = Regex.Match(sizeText, @"(\d+(\.\d+)?)\s?(MB|GB|TB)", RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var value = double.Parse(match.Groups[1].Value);
            var unit = match.Groups[3].Value.ToUpper();

            switch (unit)
            {
                case "MB":
                    size = value;
                    break;
                case "GB":
                    size = value * 1024;
                    break;
                case "TB":
                    size = value * 1024 * 1024;
                    break;
            }

            return true;
        }

        return false;
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
