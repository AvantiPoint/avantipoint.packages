window.apFeedProtocol = (function () {
  var cookieName = "ap-feed-protocol";
  function basePath() {
    var element = document.querySelector("base[href]");
    if (!element) {
      return "";
    }
    try {
      var value = new URL(element.href, window.location.origin).pathname;
      return value === "/" ? "" : value.replace(/\/$/, "");
    } catch (e) {
      return "";
    }
  }

  function fromHref(href) {
    var path = href || "/";
    try {
      path = new URL(href || "/", window.location.origin).pathname;
      var root = basePath();
      if (root && (path === root || path.indexOf(root + "/") === 0)) {
        path = path.substring(root.length) || "/";
      }
    } catch (e) {
      path = href || "/";
    }
    if (path.length > 1 && path.charAt(path.length - 1) === "/") {
      path = path.slice(0, -1);
    }
    var lower = path.toLowerCase();
    if (lower.indexOf("/browse/n" + "pm") === 0) {
      return "n" + "pm";
    }
    if (lower === "/o" + "ci") {
      return "o" + "ci";
    }
    if (lower.indexOf("/o" + "ci/") === 0) {
      var rest = path.substring(5);
      var first = rest.split("/")[0];
      if (!first || first.toLowerCase() === "repos") {
        return "o" + "ci";
      }
      return "o" + "ci:" + first;
    }
    return "nuget";
  }

  function set(value) {
    if (!value) {
      return;
    }
    document["cookie"] = cookieName + "=" + encodeURIComponent(value) + "; path=" + (basePath() || "/") + "; max-age=31536000; SameSite=Lax";
  }

  function change(select) {
    var href = select && select.value;
    if (!href) {
      return;
    }
    set(fromHref(href));
    window.location.assign(href);
  }

  return { cookieName: cookieName, fromHref: fromHref, set: set, change: change };
})();
