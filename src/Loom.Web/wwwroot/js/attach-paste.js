// Paste-to-upload glue for FeatureWorkspace.razor's artifact attach
// panel. Listens for `paste` events on the dropzone element and
// forwards image clipboard items to a Blazor [JSInvokable] method as
// base64. Pure-string interop because Blazor Server's byte[] interop
// is awkward; size cap is enforced server-side.
//
// Drop is handled natively — the dropzone wraps an <InputFile> whose
// underlying <input type="file"> already accepts dropped files.

(function () {
    if (window.loomAttachPaste) return; // module already loaded

    const listeners = new Map(); // elementId -> { handler, dotNetRef }

    function bytesToBase64(bytes) {
        let binary = '';
        const chunk = 0x8000;
        for (let i = 0; i < bytes.length; i += chunk) {
            binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
        }
        return btoa(binary);
    }

    window.loomAttachPaste = function (elementId, dotNetRef, methodName) {
        const el = document.getElementById(elementId);
        if (!el) return;
        if (listeners.has(elementId)) return;

        const handler = async function (ev) {
            const items = ev.clipboardData && ev.clipboardData.items;
            if (!items) return;
            for (let i = 0; i < items.length; i++) {
                const item = items[i];
                if (item.kind !== 'file') continue;
                const blob = item.getAsFile();
                if (!blob) continue;
                if (!blob.type || !blob.type.startsWith('image/')) continue;
                ev.preventDefault();
                const buf = await blob.arrayBuffer();
                const b64 = bytesToBase64(new Uint8Array(buf));
                const ext = blob.type.split('/')[1] || 'png';
                const filename = blob.name && blob.name.length > 0
                    ? blob.name
                    : `pasted-${Date.now()}.${ext}`;
                try {
                    await dotNetRef.invokeMethodAsync(methodName, filename, blob.type, b64);
                } catch (e) {
                    console.error('loomAttachPaste invoke failed', e);
                }
                break; // one image per paste is enough
            }
        };

        // Listen on the document so the user doesn't have to focus the
        // dropzone first — a paste anywhere on the page while the
        // dropzone is visible counts.
        document.addEventListener('paste', handler);
        listeners.set(elementId, { handler, dotNetRef });
    };

    window.loomAttachPasteOff = function (elementId) {
        const entry = listeners.get(elementId);
        if (!entry) return;
        document.removeEventListener('paste', entry.handler);
        listeners.delete(elementId);
    };
})();
