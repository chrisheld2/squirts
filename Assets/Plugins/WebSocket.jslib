mergeInto(LibraryManager.library, {


    WebSocketConnect: function (url) {
        console.log('v1.0.2');
        var ws = new WebSocket(UTF8ToString(url));

        ws.onopen = function () {
            console.log('WebSocket connection opened.');
        };

        ws.onmessage = function (event) {
            console.log('WebSocket message received:', event.data);
            SendMessage('NETStaticCom', 'HandleMessageReceived', event.data);
        };

        ws.onclose = function () {
            console.log('WebSocket connection closed.');
        };

        ws.onerror = function (error) {
            console.error('WebSocket error:', error);
        };

        window.websocket = ws;
    },

    WebSocketSend: function (message) {
        if (window.websocket && window.websocket.readyState === WebSocket.OPEN) {
            window.websocket.send(UTF8ToString(message));
        } else {
            console.error('WebSocket is not open. Current state:', window.websocket ? window.websocket.readyState : 'undefined');
        }
    },

    WebSocketClose: function () {
        if (window.websocket) {
            window.websocket.close();
            window.websocket = null;
        }
    }
});
