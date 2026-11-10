window.todoListData = {
    downloadText: function (fileName, mimeType, content) {
        var blob = new Blob([content], { type: mimeType + ";charset=utf-8" });
        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");

        link.href = url;
        link.download = fileName;
        link.rel = "noopener";

        document.body.appendChild(link);
        link.click();
        link.remove();

        URL.revokeObjectURL(url);
    }
};
