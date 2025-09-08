// PDF変換用のJavaScriptライブラリ
window.pdfConverter = {
    
    // ファイルをダウンロードする関数
    downloadFile: function(base64Data, fileName) {
        try {
            // Base64データをBlobに変換
            const byteCharacters = atob(base64Data);
            const byteNumbers = new Array(byteCharacters.length);
            for (let i = 0; i < byteCharacters.length; i++) {
                byteNumbers[i] = byteCharacters.charCodeAt(i);
            }
            const byteArray = new Uint8Array(byteNumbers);
            const blob = new Blob([byteArray], { type: 'application/zip' });
            
            // ダウンロードリンクを作成
            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            window.URL.revokeObjectURL(url);
            
            return true;
        } catch (error) {
            console.error('ダウンロードエラー:', error);
            throw new Error('ファイルのダウンロードに失敗しました: ' + error.message);
        }
    }
};

// ファイル選択ダイアログを開く関数
window.openFileDialog = function(elementRef) {
    elementRef.click();
};

// ファイル入力要素をクリックする関数
window.clickFileInput = function(elementRef) {
    elementRef.click();
};

// 選択されたファイルを取得する関数
window.getSelectedFiles = function(elementRef) {
    const files = elementRef.files;
    const fileList = [];
    
    for (let i = 0; i < files.length; i++) {
        const file = files[i];
        fileList.push({
            name: file.name,
            size: file.size,
            type: file.type,
            lastModified: file.lastModified,
            stream: file
        });
    }
    
    return fileList;
};

// ファイル選択時の処理
window.handleFileSelect = function(elementRef, dotNetHelper) {
    const files = elementRef.files;
    const fileList = [];
    
    for (let i = 0; i < files.length; i++) {
        const file = files[i];
        if (file.name.toLowerCase().endsWith('.zip')) {
            fileList.push({
                name: file.name,
                size: file.size,
                type: file.type,
                lastModified: file.lastModified,
                stream: file
            });
        }
    }
    
    // Blazorにファイル情報を渡す
    dotNetHelper.invokeMethodAsync('OnFilesSelected', fileList);
};

// ドラッグ&ドロップの処理
window.setupDragAndDrop = function(elementRef, dotNetHelper) {
    const dropZone = elementRef;
    
    dropZone.addEventListener('dragover', function(e) {
        e.preventDefault();
        e.stopPropagation();
        dotNetHelper.invokeMethodAsync('OnDragOver');
    });
    
    dropZone.addEventListener('dragleave', function(e) {
        e.preventDefault();
        e.stopPropagation();
        dotNetHelper.invokeMethodAsync('OnDragLeave');
    });
    
    dropZone.addEventListener('drop', function(e) {
        e.preventDefault();
        e.stopPropagation();
        
        const files = e.dataTransfer.files;
        const fileList = [];
        let processedFiles = 0;
        const totalFiles = files.length;
        
        if (totalFiles === 0) {
            return;
        }
        
        for (let i = 0; i < files.length; i++) {
            const file = files[i];
            if (file.name.toLowerCase().endsWith('.zip')) {
                // ファイルをBase64に変換してBlazorに渡す
                const reader = new FileReader();
                reader.onload = function(event) {
                    try {
                        const base64 = event.target.result.split(',')[1];
                        const fileData = {
                            name: file.name,
                            size: file.size,
                            type: file.type,
                            lastModified: file.lastModified,
                            base64: base64
                        };
                        fileList.push(fileData);
                        processedFiles++;
                        
                        if (processedFiles === totalFiles) {
                            dotNetHelper.invokeMethodAsync('OnFilesDropped', fileList);
                        }
                    } catch (error) {
                        console.error('ファイル読み込みエラー:', error);
                        processedFiles++;
                        if (processedFiles === totalFiles) {
                            dotNetHelper.invokeMethodAsync('OnFilesDropped', fileList);
                        }
                    }
                };
                reader.onerror = function(error) {
                    console.error('ファイル読み込みエラー:', error);
                    processedFiles++;
                    if (processedFiles === totalFiles) {
                        dotNetHelper.invokeMethodAsync('OnFilesDropped', fileList);
                    }
                };
                reader.readAsDataURL(file);
            } else {
                processedFiles++;
                if (processedFiles === totalFiles) {
                    dotNetHelper.invokeMethodAsync('OnFilesDropped', fileList);
                }
            }
        }
    });
};

// Blazorから呼び出し可能な関数を定義
window.convertHtmlToPdf = window.pdfConverter.convertHtmlToPdf;
window.downloadFile = window.pdfConverter.downloadFile;
