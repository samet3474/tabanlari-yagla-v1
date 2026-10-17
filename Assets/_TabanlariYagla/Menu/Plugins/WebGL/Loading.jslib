// The page's own loading screen (#ty-loader in WebGLTemplates/TabanlariYagla/index.html) stays up until the main menu
// is ready: the game reports its own loading here, so one bar fills from 0 to 100 (download first, then the game's
// loading), and the screen closes when the main menu opens. Used by StartButton.
mergeInto(LibraryManager.library, {
  TY_LoadingProgress: function (fraction) {
    if (window.tyLoadingProgress) window.tyLoadingProgress(fraction);
  },
  TY_LoadingDone: function () {
    if (window.tyLoadingDone) window.tyLoadingDone();
  }
});
