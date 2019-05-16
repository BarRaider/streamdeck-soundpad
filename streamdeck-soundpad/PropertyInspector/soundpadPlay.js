document.addEventListener('websocketCreate', function () {
    console.log("Websocket created!");
    loadConfiguration(actionInfo.payload.settings);

    websocket.addEventListener('message', function (event) {
        console.log("Got message event!");

        // Received message from Stream Deck
        var jsonObj = JSON.parse(event.data);

        if (jsonObj.event === 'sendToPropertyInspector') {
            var payload = jsonObj.payload;
            loadConfiguration(payload);
        }
        else if (jsonObj.event === 'didReceiveSettings') {
            var payload = jsonObj.payload;
            loadConfiguration(payload.settings);
        }
    });
});

function loadConfiguration(payload) {
    console.log('loadConfiguration');
    console.log(payload);

    if (payload["sounds"]) {
        var items = payload["sounds"];
        var sounds = document.getElementById('sounds')
        sounds.options.length = 0;

        for (var idx = 0; idx < items.length; idx++) {
            var opt = document.createElement('option');
            opt.value = items[idx].soundName;
            opt.text = items[idx].soundName;
            sounds.appendChild(opt);
        }
        sounds.value = payload["soundTitle"];
    }

    var showSoundTitle = document.getElementById('showSoundTitle');
    showSoundTitle.checked = payload['showSoundTitle'];
}

function setSettings() {
    var payload = {};
    var sounds = document.getElementById('sounds');
    var showSoundTitle = document.getElementById('showSoundTitle');

    var payload = {};

    payload.property_inspector = 'updateSettings';
    payload.soundTitle = sounds.value;
    payload.showSoundTitle = showSoundTitle.checked;

    setSettingsToPlugin(payload);
    console.log("Saved Settings");
}
