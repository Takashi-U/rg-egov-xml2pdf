# e-Gov電子公文書 HTML変換ツール

e-Gov電子申請で返却される電子公文書（ZIPファイル）を、ユーザーが見やすいHTMLファイルに変換するBlazor WebAssemblyアプリケーションです。

## 機能

- **ファイルアップロード**: ドラッグ&ドロップまたはファイル選択で電子公文書（ZIPファイル）をアップロード
- **XML/XSL変換**: XMLファイルにXSLスタイルシートを適用してHTMLに変換
- **クライアントサイド処理**: すべての処理をブラウザ内で実行（セキュリティ重視）
- **一括処理**: 複数の電子公文書を同時に処理
- **結果ダウンロード**: 変換されたHTMLファイルを含むZIPファイルをダウンロード

## 技術仕様

- **フレームワーク**: Blazor WebAssembly (.NET 8)
- **UI**: Bootstrap 5 + Font Awesome
- **処理**: XML/XSL変換（System.Xml.Xsl）
- **デプロイ**: Azure Static Web Apps

## 使用方法

1. 電子公文書（ZIPファイル）をダウンロード
2. アプリケーションにドラッグ&ドロップまたはファイル選択でアップロード
3. 「変換開始」ボタンをクリック
4. 処理完了後、結果のZIPファイルをダウンロード

## 開発環境

- .NET 8 SDK
- Visual Studio 2022 または VS Code

## ビルドと実行

```bash
# 依存関係の復元
dotnet restore

# アプリケーションの実行
dotnet run

# リリースビルド
dotnet publish -c Release -o ./publish
```

## デプロイ

このアプリケーションはAzure Static Web Appsにデプロイされています。

### 自動デプロイ

GitHubリポジトリにプッシュすると、自動的にAzure Static Web Appsにデプロイされます。

### 手動デプロイ

```bash
# ビルド
dotnet publish -c Release -o ./publish

# Azure Static Web Apps CLIでデプロイ
swa deploy ./publish
```

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。

## 注意事項

- このアプリケーションはクライアントサイドでのみ動作し、サーバーにデータを送信しません
- 電子公文書の処理はすべてブラウザ内で実行されます
- 大きなファイルの処理には時間がかかる場合があります
