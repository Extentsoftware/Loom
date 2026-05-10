// Paste-to-upload glue for FeatureWorkspace.razor's artifact attach
// panel. Listens for `paste` events on the window (capture phase) and
// forwards image clipboard items to a Blazor [JSInvokable] method as
// base64. Pure-string interop because Blazor Server's byte[] interop
// is awkward; size cap is enforced server-side.

(function () {
    if (window.loomAttachPaste) return; // module already loaded

    console.log('[loom] attach-paste.js loaded');
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
                console.log('[loom] paste image', blob.type, blob.size);
                try {
                    const buf = await blob.arrayBuffer();
                    const b64 = bytesToBase64(new Uint8Array(buf));
                    const ext = blob.type.split('/')[1] || 'png';
                    const filename = blob.name && blob.name.length > 0
                        ? blob.name
                        : `pasted-${Date.now()}.${ext}`;
                    await dotNetRef.invokeMethodAsync(methodName, filename, blob.type, b64);
                } catch (e) {
                    console.error('[loom] paste invoke failed', e);
                }
                break; // one image per paste
            }
        };

        // Listen on window in capture phase so we fire even when the
        // active element is something Blazor renders later (a
        // <button>, a <select>, etc.) and even when no editable
        // element has focus.
        window.addEventListener('paste', handler, true);
        listeners.set(elementId, { handler, dotNetRef });
        console.log('[loom] paste listener attached for', elementId);
    };

    window.loomAttachPasteOff = function (elementId) {
        const entry = listeners.get(elementId);
        if (!entry) return;
        window.removeEventListener('paste', entry.handler, true);
        listeners.delete(elementId);
    };
})();
