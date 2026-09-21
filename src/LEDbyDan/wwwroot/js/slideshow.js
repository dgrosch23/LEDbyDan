(function () {
    "use strict";

    var dataEl = document.getElementById("media-data");
    var items = dataEl ? JSON.parse(dataEl.textContent || "[]") : [];
    if (items.length === 0) {
        return;
    }

    var root = document.getElementById("slideshow");
    var mediaContainer = document.getElementById("mediaContainer");
    var counterEl = document.getElementById("counter");
    var prevBtn = document.getElementById("prevBtn");
    var nextBtn = document.getElementById("nextBtn");
    var fullscreenBtn = document.getElementById("fullscreenBtn");

    var startIndex = parseInt(root.getAttribute("data-start-index"), 10);
    var currentIndex = (isNaN(startIndex) || startIndex < 0 || startIndex >= items.length) ? 0 : startIndex;

    var preloaded = {};

    function preloadImage(index) {
        var item = items[index];
        if (!item || item.type !== "image" || preloaded[item.url]) {
            return;
        }
        preloaded[item.url] = true;
        var img = new Image();
        img.src = item.url;
    }

    function render(index) {
        currentIndex = ((index % items.length) + items.length) % items.length;
        var item = items[currentIndex];

        mediaContainer.innerHTML = "";

        if (item.type === "video") {
            var video = document.createElement("video");
            video.src = item.url;
            video.controls = true;
            video.preload = "metadata";
            video.playsInline = true;
            mediaContainer.appendChild(video);
        } else {
            var img = document.createElement("img");
            img.src = item.url;
            img.alt = item.name || "";
            mediaContainer.appendChild(img);
        }

        counterEl.textContent = (currentIndex + 1) + " / " + items.length;

        var url = new URL(window.location.href);
        url.searchParams.set("i", currentIndex);
        window.history.replaceState(null, "", url.pathname + url.search);

        preloadImage(currentIndex + 1);
        preloadImage(currentIndex - 1);
    }

    function goTo(index) {
        // Pause/stop any playing video before swapping content.
        var playing = mediaContainer.querySelector("video");
        if (playing) {
            playing.pause();
        }
        render(index);
    }

    prevBtn.addEventListener("click", function () { goTo(currentIndex - 1); });
    nextBtn.addEventListener("click", function () { goTo(currentIndex + 1); });

    document.addEventListener("keydown", function (e) {
        if (e.key === "ArrowLeft") {
            goTo(currentIndex - 1);
        } else if (e.key === "ArrowRight") {
            goTo(currentIndex + 1);
        } else if (e.key === "Escape" && document.fullscreenElement) {
            document.exitFullscreen();
        }
    });

    // Basic swipe support for mobile/touch devices.
    var touchStartX = null;
    var touchStartY = null;
    var stage = document.getElementById("stage");

    stage.addEventListener("touchstart", function (e) {
        var t = e.changedTouches[0];
        touchStartX = t.clientX;
        touchStartY = t.clientY;
    }, { passive: true });

    stage.addEventListener("touchend", function (e) {
        if (touchStartX === null) {
            return;
        }
        var t = e.changedTouches[0];
        var dx = t.clientX - touchStartX;
        var dy = t.clientY - touchStartY;
        touchStartX = null;
        touchStartY = null;

        var minSwipeDistance = 40;
        if (Math.abs(dx) > minSwipeDistance && Math.abs(dx) > Math.abs(dy)) {
            if (dx < 0) {
                goTo(currentIndex + 1);
            } else {
                goTo(currentIndex - 1);
            }
        }
    }, { passive: true });

    if (fullscreenBtn) {
        if (!document.documentElement.requestFullscreen) {
            fullscreenBtn.style.display = "none";
        } else {
            fullscreenBtn.addEventListener("click", function () {
                if (document.fullscreenElement) {
                    document.exitFullscreen();
                } else {
                    root.requestFullscreen().catch(function () { /* ignored: not fatal */ });
                }
            });
        }
    }

    render(currentIndex);
})();
