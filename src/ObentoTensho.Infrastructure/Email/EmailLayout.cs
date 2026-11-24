using System.Net;

namespace ObentoTensho.Infrastructure.Email;

// Khung HTML chung: CSS nội tuyến (ứng dụng mail bỏ <style> ngoài), rộng tối đa 560px, đọc tốt trên điện thoại
internal static class EmailLayout
{
    public static string Wrap(string language, string title, string bodyHtml) => $$"""
        <!doctype html>
        <html lang="{{language}}">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{WebUtility.HtmlEncode(title)}}</title>
        <style>
        .button { display: inline-block; padding: 12px 20px; background: #a81c1f; color: #ffffff !important; text-decoration: none; border-radius: 8px; }
        </style>
        </head>
        <body style="margin:0;padding:0;background:#f6f2ec;font-family:Arial,'Noto Sans','Hiragino Sans',sans-serif;color:#3a2115;line-height:1.6">
        <div style="max-width:560px;margin:0 auto;padding:24px 16px">
        <div style="font-size:20px;font-weight:600;color:#a81c1f;margin-bottom:16px">Obento Tensho</div>
        <div style="background:#ffffff;border-radius:8px;padding:24px">
        {{bodyHtml}}
        </div>
        </div>
        </body>
        </html>
        """;
}
