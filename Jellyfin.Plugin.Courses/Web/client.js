(function () {
    'use strict';

    var PLUGIN_ID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890';
    var COURSE_PATHS = null;


    function getAuth() {
        try {
            var creds = JSON.parse(localStorage.getItem('jellyfin_credentials') || '{}');
            var server = (creds.Servers || [])[0];
            if (server && server.AccessToken && server.UserId) {
                return { token: server.AccessToken, userId: server.UserId };
            }
        } catch (e) { }
        return null;
    }

    function apiFetch(path) {
        var auth = getAuth();
        if (!auth) return Promise.reject(new Error('Not authenticated'));
        return fetch(window.location.origin + path, {
            headers: { 'Authorization': 'MediaBrowser Token="' + auth.token + '"' }
        }).then(function (r) {
            if (!r.ok) throw new Error('API error: ' + r.status);
            if (r.status === 204) return null;
            return r.json();
        });
    }

    function esc(s) {
        var d = document.createElement('div');
        d.textContent = s || '';
        return d.innerHTML;
    }

    function formatDuration(ticks) {
        if (!ticks) return '';
        var secs = Math.floor(ticks / 10000000);
        var m = Math.floor(secs / 60);
        var s = secs % 60;
        if (m >= 60) {
            var h = Math.floor(m / 60);
            m = m % 60;
            return h + 'h ' + m + 'm';
        }
        return m + ':' + (s < 10 ? '0' : '') + s;
    }

    function g(obj, key) {
        return obj[key] !== undefined ? obj[key] : obj[key.charAt(0).toLowerCase() + key.slice(1)];
    }

    // Collect all lesson IDs from structure data, flattened across sections.
    function getAllLessonIds(sections) {
        var ids = [];
        for (var i = 0; i < sections.length; i++) {
            var lessons = g(sections[i], 'Lessons') || [];
            for (var j = 0; j < lessons.length; j++) {
                ids.push(g(lessons[j], 'Id'));
            }
        }
        return ids;
    }

    // Get lesson IDs starting from the first unplayed one.
    function getLessonIdsFromContinue(sections) {
        var ids = [];
        var foundUnplayed = false;
        for (var i = 0; i < sections.length; i++) {
            var lessons = g(sections[i], 'Lessons') || [];
            for (var j = 0; j < lessons.length; j++) {
                if (!foundUnplayed && !g(lessons[j], 'Played')) {
                    foundUnplayed = true;
                }
                if (foundUnplayed) {
                    ids.push(g(lessons[j], 'Id'));
                }
            }
        }
        // All played — return all for replay.
        if (!foundUnplayed) return getAllLessonIds(sections);
        return ids;
    }

    // Callback set by init() — clears lastUrl so the overview refreshes
    // when the user returns to the course page after playback.
    var invalidateOverview = function () {};

    // Play a list of item IDs using the Sessions API.
    function playItems(itemIds) {
        if (!itemIds || !itemIds.length) return;
        var auth = getAuth();
        if (!auth) return;
        // Mark overview as stale so it refreshes after playback.
        invalidateOverview();
        apiFetch('/Sessions?ControllableByUserId=' + auth.userId)
            .then(function (sessions) {
                if (!sessions || !sessions.length) return;
                var sessionId = sessions[0].Id;
                var idsParam = itemIds.join(',');
                return fetch(window.location.origin + '/Sessions/' + sessionId + '/Playing?playCommand=PlayNow&itemIds=' + idsParam, {
                    method: 'POST',
                    headers: { 'Authorization': 'MediaBrowser Token="' + auth.token + '"' },
                });
            })
            .catch(function () { });
    }

    function getItemIdFromUrl() {
        var hash = window.location.hash || '';
        var idMatch = hash.match(/[?&]id=([a-f0-9]+)/i);
        if (idMatch) return idMatch[1];
        var parentMatch = hash.match(/[?&]parentId=([a-f0-9]+)/i);
        if (parentMatch) return parentMatch[1];
        return null;
    }

    function isCourseItem(item) {
        var itemPath = item.Path || '';
        if (!itemPath || !COURSE_PATHS || !COURSE_PATHS.length) return false;
        for (var i = 0; i < COURSE_PATHS.length; i++) {
            if (itemPath.indexOf(COURSE_PATHS[i]) === 0) return true;
        }
        return false;
    }

    function injectCourseOverview(itemId) {
        var auth = getAuth();
        if (!auth) return;
        if (document.querySelector('.courses-plugin-overview')) return;

        apiFetch('/Courses/' + itemId + '/Structure?userId=' + auth.userId + '&_t=' + Date.now())
            .then(function (data) {
                if (!data) return;
                renderOverview(data, itemId);
            })
            .catch(function (err) {
                console.warn('[Courses] Structure fetch failed:', err);
            });
    }

    function renderOverview(data, courseId) {
        if (document.querySelector('.courses-plugin-overview')) return;

        var selectors = [
            '.page:not(.hide) .itemsContainer',
            '.page:not(.hide) .padded-left',
            '.page:not(.hide) [data-role="content"]',
            '.detailPageContent',
            '#reactRoot .page:not(.hide)',
            '#reactRoot [class*="Page"]',
        ];
        var target = null;
        for (var i = 0; i < selectors.length; i++) {
            target = document.querySelector(selectors[i]);
            if (target) break;
        }
        if (!target) return;

        var sections = g(data, 'Sections') || [];
        var total = g(data, 'TotalLessons') || 0;
        var completed = g(data, 'CompletedLessons') || 0;
        var pct = g(data, 'ProgressPercent') || 0;

        var container = document.createElement('div');
        container.className = 'courses-plugin-overview';

        var html = '<style>'
            + '.courses-plugin-overview { padding: 0 1em 1em; }'
            + '.cp-progress-header { display: flex; align-items: center; gap: 16px; margin-bottom: 12px; flex-wrap: wrap; }'
            + '.cp-progress-text { color: #999; font-size: 0.9em; }'
            + '.cp-progress-bar { flex: 1; min-width: 120px; background: #333; border-radius: 4px; height: 6px; overflow: hidden; }'
            + '.cp-progress-fill { background: #00a4dc; height: 100%; transition: width 0.3s; }'
            + '.cp-continue-btn { background: #00a4dc; color: #fff; border: none; padding: 8px 20px; border-radius: 4px; cursor: pointer; font-size: 0.9em; }'
            + '.cp-continue-btn:hover { background: #0090c4; }'
            + '.cp-section { background: #1a1a1a; border-radius: 6px; margin-bottom: 8px; overflow: hidden; }'
            + '.cp-section-hdr { padding: 10px 14px; cursor: pointer; display: flex; justify-content: space-between; align-items: center; }'
            + '.cp-section-hdr:hover { background: #222; }'
            + '.cp-section-hdr h3 { margin: 0; font-size: 1em; font-weight: 500; color: #eee; }'
            + '.cp-section-count { color: #999; font-size: 0.8em; margin-left: 8px; }'
            + '.cp-section-arrow { color: #666; transition: transform 0.2s; display: inline-block; }'
            + '.cp-section-hdr.open .cp-section-arrow { transform: rotate(90deg); }'
            + '.cp-lessons { display: none; padding: 0 14px 8px; }'
            + '.cp-lessons.open { display: block; }'
            + '.cp-lesson { display: flex; align-items: center; padding: 6px 0; border-bottom: 1px solid #252525; gap: 8px; }'
            + '.cp-lesson:last-child { border-bottom: none; }'
            + '.cp-lesson-status { width: 18px; text-align: center; flex-shrink: 0; }'
            + '.cp-lesson-status.played { color: #4caf50; }'
            + '.cp-lesson-status.unplayed { color: #555; }'
            + '.cp-lesson-name { flex: 1; }'
            + '.cp-lesson-name a { color: #ddd; text-decoration: none; }'
            + '.cp-lesson-name a:hover { color: #00a4dc; }'
            + '.cp-lesson-dur { color: #777; font-size: 0.8em; }'
            + '</style>';

        html += '<div class="cp-progress-header">'
            + '<button class="cp-continue-btn" id="cpContinueBtn">'
            + (completed >= total && total > 0 ? 'Replay Course' : 'Continue Course')
            + '</button>'
            + '<span class="cp-progress-text">' + completed + ' / ' + total + ' lessons</span>'
            + '<div class="cp-progress-bar"><div class="cp-progress-fill" style="width:' + pct + '%"></div></div>'
            + '</div>';

        for (var i = 0; i < sections.length; i++) {
            var sec = sections[i];
            var sName = g(sec, 'Name');
            var sLessons = g(sec, 'Lessons') || [];
            var sCompleted = g(sec, 'CompletedCount') || 0;
            var sTotal = g(sec, 'TotalCount') || 0;

            html += '<div class="cp-section">'
                + '<div class="cp-section-hdr" data-cpidx="' + i + '">'
                + '<h3>' + esc(sName) + '<span class="cp-section-count">' + sCompleted + '/' + sTotal + '</span></h3>'
                + '<span class="cp-section-arrow">&#9654;</span>'
                + '</div>'
                + '<div class="cp-lessons" data-cpidx="' + i + '">';

            for (var j = 0; j < sLessons.length; j++) {
                var l = sLessons[j];
                var lId = g(l, 'Id');
                var lName = g(l, 'Name');
                var played = g(l, 'Played');
                var rt = g(l, 'RunTimeTicks') || 0;

                html += '<div class="cp-lesson">'
                    + '<div class="cp-lesson-status ' + (played ? 'played' : 'unplayed') + '">'
                    + (played ? '&#10003;' : '&#9675;') + '</div>'
                    + '<div class="cp-lesson-name"><a href="#!/details?id=' + lId + '">' + esc(lName) + '</a></div>'
                    + '<div class="cp-lesson-dur">' + formatDuration(rt) + '</div>'
                    + '</div>';
            }

            html += '</div></div>';
        }

        container.innerHTML = html;
        target.parentNode.insertBefore(container, target);

        // Toggle sections.
        container.querySelectorAll('.cp-section-hdr').forEach(function (hdr) {
            hdr.addEventListener('click', function () {
                var idx = this.getAttribute('data-cpidx');
                var lessons = container.querySelector('.cp-lessons[data-cpidx="' + idx + '"]');
                lessons.classList.toggle('open');
                this.classList.toggle('open');
            });
        });

        // Auto-open first uncompleted section.
        for (var k = 0; k < sections.length; k++) {
            if ((g(sections[k], 'CompletedCount') || 0) < (g(sections[k], 'TotalCount') || 0)) {
                var hdr = container.querySelector('.cp-section-hdr[data-cpidx="' + k + '"]');
                if (hdr) hdr.click();
                break;
            }
        }

        // Continue / Replay: queue all lessons from the continue point onward.
        var btn = document.getElementById('cpContinueBtn');
        if (btn) {
            btn.addEventListener('click', function () {
                var ids = (completed >= total && total > 0)
                    ? getAllLessonIds(sections)
                    : getLessonIdsFromContinue(sections);
                playItems(ids);
            });
        }
    }

    function cleanup() {
        var existing = document.querySelector('.courses-plugin-overview');
        if (existing) existing.remove();
    }

    function init() {
        var lastUrl = '';
        var activeObserver = null;

        // Allow playItems to invalidate the cached URL so the overview
        // re-fetches with fresh progress data after playback.
        invalidateOverview = function () { lastUrl = ''; };

        // Wait for an injection target to appear, then inject.
        // Uses a short-lived MutationObserver scoped to this one page load.
        function waitAndInject(itemId) {
            // Already injected?
            if (document.querySelector('.courses-plugin-overview')) return;

            // Try immediately first.
            var target = document.querySelector('.page:not(.hide) .itemsContainer')
                || document.querySelector('.page:not(.hide) .padded-left')
                || document.querySelector('.detailPageContent');
            if (target) {
                injectCourseOverview(itemId);
                return;
            }

            // Not ready yet — observe until it appears.
            if (activeObserver) activeObserver.disconnect();
            var root = document.querySelector('#reactRoot') || document.body;
            activeObserver = new MutationObserver(function () {
                if (document.querySelector('.courses-plugin-overview')) {
                    activeObserver.disconnect();
                    activeObserver = null;
                    return;
                }
                var t = document.querySelector('.page:not(.hide) .itemsContainer')
                    || document.querySelector('.page:not(.hide) .padded-left')
                    || document.querySelector('.detailPageContent');
                if (t) {
                    activeObserver.disconnect();
                    activeObserver = null;
                    injectCourseOverview(itemId);
                }
            });
            activeObserver.observe(root, { childList: true, subtree: true });

            // Safety: disconnect after 10s no matter what.
            setTimeout(function () {
                if (activeObserver) {
                    activeObserver.disconnect();
                    activeObserver = null;
                }
            }, 10000);
        }

        function onPageChange() {
            var url = window.location.hash;
            if (url === lastUrl) return;
            lastUrl = url;
            cleanup();
            if (activeObserver) {
                activeObserver.disconnect();
                activeObserver = null;
            }

            var itemId = getItemIdFromUrl();
            if (!itemId) return;
            if (!COURSE_PATHS || !COURSE_PATHS.length) return;

            apiFetch('/Items/' + itemId + '?Fields=Path').then(function (item) {
                if (!item) return;
                var isFolder = item.IsFolder || item.isFolder;
                if (isCourseItem(item) && isFolder) {
                    waitAndInject(itemId);
                }
            }).catch(function () { });
        }

        // Refresh overview when returning from playback (tab becomes visible again,
        // or player overlay closes and React re-renders).
        document.addEventListener('visibilitychange', function () {
            if (!document.hidden) onPageChange();
        });

        // Listen for all forms of URL change:
        // - hashchange: direct hash changes
        // - popstate: back/forward navigation
        // - pushState/replaceState: SPA navigation (React Router)
        window.addEventListener('hashchange', onPageChange);
        window.addEventListener('popstate', onPageChange);

        // Monkey-patch pushState/replaceState to detect SPA navigation.
        var origPush = history.pushState;
        var origReplace = history.replaceState;
        history.pushState = function () {
            origPush.apply(this, arguments);
            onPageChange();
        };
        history.replaceState = function () {
            origReplace.apply(this, arguments);
            onPageChange();
        };

        onPageChange();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
