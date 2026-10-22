function apCopy(btn) {
  var text = btn.getAttribute("data-copy") || "";
  if (!navigator.clipboard) return;
  navigator.clipboard.writeText(text).then(function () {
    var prev = btn.textContent;
    btn.textContent = "Copied";
    setTimeout(function () { btn.textContent = prev; }, 1500);
  });
}
