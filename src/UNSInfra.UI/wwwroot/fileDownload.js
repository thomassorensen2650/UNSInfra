window.downloadFileFromStream = (fileName, contentStreamReference) => {
    const arrayBuffer = new ArrayBuffer(contentStreamReference.byteLength);
    const uint8Array = new Uint8Array(arrayBuffer);
    for (let i = 0; i < contentStreamReference.byteLength; i++) {
        uint8Array[i] = contentStreamReference[i];
    }

    const blob = new Blob([uint8Array]);
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};

window.downloadFileFromString = (fileName, content) => {
    const blob = new Blob([content], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};