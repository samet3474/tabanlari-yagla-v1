// Safe area insets of the page (CSS env(safe-area-inset-*), non-zero only with viewport-fit=cover on phones with a
// notch, Dynamic Island, camera hole or home indicator), in canvas pixels. Used by SafeAreaFitter.
mergeInto(LibraryManager.library, {
  TY_SafeAreaInset: function (side) {
    if (!window.tySafeAreaProbe) {
      var probe = document.createElement('div');
      probe.style.cssText = 'position:fixed;left:0;top:0;width:0;height:0;visibility:hidden;pointer-events:none;' +
        'padding-top:env(safe-area-inset-top,0px);padding-right:env(safe-area-inset-right,0px);' +
        'padding-bottom:env(safe-area-inset-bottom,0px);padding-left:env(safe-area-inset-left,0px);';
      document.body.appendChild(probe);
      window.tySafeAreaProbe = probe;
    }
    var style = window.getComputedStyle(window.tySafeAreaProbe);
    var css = [style.paddingTop, style.paddingRight, style.paddingBottom, style.paddingLeft][side];
    var cssPixels = parseFloat(css) || 0;
    // Canvas pixels per CSS pixel (the canvas backing store follows devicePixelRatio).
    var canvas = Module.canvas;
    var scale = canvas && canvas.clientWidth > 0 ? canvas.width / canvas.clientWidth : (window.devicePixelRatio || 1);
    return cssPixels * scale;
  }
});
