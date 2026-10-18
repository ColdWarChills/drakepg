// A file picker for the web build: reads a save file (e.g. drakepg-save.json exported by the Phaser version) and
// hands its text to a GameObject's method (Saves.PickFile in Core/Saves.cs).
mergeInto(LibraryManager.library, {
  // Watches the pointer from the title screen on, so a pick request knows whether the button is still held.
  DrakepgPickerInit: function () {
    if (window.drakepgPicker) return;
    var state = (window.drakepgPicker = { down: false, pending: null });
    document.addEventListener('pointerdown', function () { state.down = true; }, true);
    document.addEventListener('pointerup', function () {
      state.down = false;
      var open = state.pending;
      state.pending = null;
      if (open) open();
    }, true);
  },

  DrakepgPickSaveFile: function (objectName, methodName) {
    var target = UTF8ToString(objectName);
    var method = UTF8ToString(methodName);
    var input = document.getElementById('drakepg-save-input');
    if (!input) {
      input = document.createElement('input');
      input.type = 'file';
      input.accept = '.json,.txt,application/json,text/plain';
      input.id = 'drakepg-save-input';
      input.style.display = 'none';
      document.body.appendChild(input);
    }
    input.value = '';
    input.onchange = function () {
      var file = input.files && input.files[0];
      if (!file) return;
      file.text().then(function (text) {
        SendMessage(target, method, text);
      });
    };
    // Browsers open a file picker only during or just after a click, and Unity sees a press a frame after the
    // browser does. Still held: open on the release. Already released: open now, while the click still counts.
    var open = function () { input.click(); };
    var state = window.drakepgPicker;
    if (state && state.down) {
      state.pending = open;
      setTimeout(function () { if (state.pending === open) state.pending = null; }, 3000);
    } else open();
  },
});
