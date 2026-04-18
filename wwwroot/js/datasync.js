// BroadcastChannel-based cross-tab data sync.
// Each tab generates a unique tabId to filter out self-sent messages.

const CHANNEL_NAME = 'sja-data';

let _channel = null;
let _tabId = null;
let _dotNetRef = null;

export function initialize(tabId, dotNetRef) {
    _tabId = tabId;
    _dotNetRef = dotNetRef;
    _channel = new BroadcastChannel(CHANNEL_NAME);
    _channel.onmessage = e => {
        const msg = e.data;
        if (!msg || msg.tabId === _tabId) return; // ignore self
        _dotNetRef.invokeMethodAsync('OnMessageReceived', msg.entity, msg.id ?? null, msg.event);
    };
}

export function broadcast(entity, id, eventName) {
    if (!_channel) return;
    _channel.postMessage({ entity, id: id ?? null, event: eventName, tabId: _tabId });
}

export function dispose() {
    if (_channel) { _channel.close(); _channel = null; }
    _dotNetRef = null;
}
