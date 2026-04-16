(function () {
    'use strict';

    var PLUGIN_ID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890';
    var COURSE_PATHS = null;

    // Sidebar localStorage helpers
    var SIDEBAR_OPEN_KEY = 'courses-sidebar-open';
    var SIDEBAR_WIDTH_KEY = 'courses-sidebar-width';
    var DEFAULT_WIDTH = 340;
    var MIN_WIDTH = 280;
    var MAX_WIDTH = 500;

    function getSidebarOpen() {
        var val = localStorage.getItem(SIDEBAR_OPEN_KEY);
        return val === null ? true : val === 'true';
    }
    function setSidebarOpen(open) {
        localStorage.setItem(SIDEBAR_OPEN_KEY, open ? 'true' : 'false');
    }
    function getSidebarWidth() {
        var val = parseInt(localStorage.getItem(SIDEBAR_WIDTH_KEY), 10);
        return (val >= MIN_WIDTH && val <= MAX_WIDTH) ? val : DEFAULT_WIDTH;
    }
    function setSidebarWidth(w) {
        localStorage.setItem(SIDEBAR_WIDTH_KEY, String(w));
    }

    // Module-level sidebar state
    var sidebarEl = null;
    var backdropEl = null;
    var lastCourseData = null;
    var lastCourseId = null;
    var lastProgressPct = 0;

    // DOM helpers
    function getHeaderHeight() {
        var hdr = document.querySelector('.skinHeader');
        return hdr ? hdr.offsetHeight : 0;
    }

    function getContentRoot() {
        return document.querySelector('.skinBody') || document.querySelector('#reactRoot') || document.body;
    }

    function createSidebar() {
        if (sidebarEl) return sidebarEl;
        sidebarEl = document.createElement('div');
        sidebarEl.className = 'cp-sidebar cp-hidden';
        sidebarEl.style.top = getHeaderHeight() + 'px';
        backdropEl = document.createElement('div');
        backdropEl.className = 'cp-backdrop';
        backdropEl.addEventListener('click', function() { collapseSidebar(); });
        document.body.appendChild(sidebarEl);
        document.body.appendChild(backdropEl);
        return sidebarEl;
    }

    function pushContent(marginLeft) {
        var root = getContentRoot();
        if (!root) return;
        if (!root.classList.contains('cp-content-push')) {
            root.classList.add('cp-content-push');
        }
        root.style.marginLeft = marginLeft > 0 ? marginLeft + 'px' : '';
    }

    function expandSidebar() {
        if (!sidebarEl) return;
        var width = getSidebarWidth();
        sidebarEl.className = 'cp-sidebar cp-expanded';
        sidebarEl.style.width = width + 'px';
        sidebarEl.style.top = getHeaderHeight() + 'px';
        setSidebarOpen(true);
        if (window.innerWidth < 768) {
            backdropEl.style.display = 'block';
            pushContent(0);
        } else {
            pushContent(width);
        }
    }

    function renderCollapsedTab(progressPct) {
        return '<div style="display:flex;flex-direction:column;align-items:center;padding-top:16px;height:100%;">'
            + '<div class="cp-tab-label">COURSE</div>'
            + '<div class="cp-tab-arrow">&#9656;</div>'
            + '<div style="flex:1;"></div>'
            + '<div class="cp-tab-progress">'
            + '<div class="cp-tab-pfill" style="height:' + progressPct + '%;"></div>'
            + '</div>'
            + '<div style="height:16px;"></div>'
            + '</div>';
    }

    function collapseSidebar() {
        if (!sidebarEl) return;
        sidebarEl.className = 'cp-sidebar cp-collapsed';
        sidebarEl.style.width = '';
        setSidebarOpen(false);
        backdropEl.style.display = 'none';
        if (window.innerWidth < 768) {
            pushContent(0);
        } else {
            pushContent(28);
        }
        sidebarEl.innerHTML = renderCollapsedTab(lastProgressPct);
        sidebarEl.addEventListener('click', function onTabClick() {
            sidebarEl.removeEventListener('click', onTabClick);
            expandSidebar();
            renderSidebarContent(lastCourseData, lastCourseId);
        });
    }

    function hideSidebar() {
        if (!sidebarEl) return;
        sidebarEl.className = 'cp-sidebar cp-hidden';
        sidebarEl.style.width = '';
        pushContent(0);
        backdropEl.style.display = 'none';
    }

    function renderSidebarContent(data, courseId) {
        if (!data || !sidebarEl) return;

        lastCourseData = data;
        lastCourseId = courseId;

        var sections = g(data, 'Sections') || [];
        var total = g(data, 'TotalLessons') || 0;
        var completed = g(data, 'CompletedLessons') || 0;
        var pct = g(data, 'ProgressPercent') || 0;
        var courseName = g(data, 'CourseName') || 'Course';

        lastProgressPct = pct;

        // Calculate total duration by summing all lesson RunTimeTicks.
        var totalTicks = 0;
        for (var si = 0; si < sections.length; si++) {
            var sLessons = g(sections[si], 'Lessons') || [];
            for (var li = 0; li < sLessons.length; li++) {
                totalTicks += g(sLessons[li], 'RunTimeTicks') || 0;
            }
        }
        var totalDuration = formatDuration(totalTicks);

        // Find next unplayed lesson name for the continue button.
        var nextLessonName = '';
        for (var ni = 0; ni < sections.length; ni++) {
            var nLessons = g(sections[ni], 'Lessons') || [];
            for (var nj = 0; nj < nLessons.length; nj++) {
                if (!g(nLessons[nj], 'Played')) {
                    nextLessonName = g(nLessons[nj], 'Name') || '';
                    break;
                }
            }
            if (nextLessonName) break;
        }

        var initial = courseName.charAt(0).toUpperCase();

        // --- Build HTML ---
        var html = '';

        // Header
        html += '<div class="cp-header">'
            + '<div style="display:flex;align-items:center;justify-content:space-between;">'
            + '<div style="display:flex;align-items:center;gap:8px;">'
            + '<div class="cp-course-icon">' + esc(initial) + '</div>'
            + '<div>'
            + '<div class="cp-course-name">' + esc(courseName) + '</div>'
            + '<div class="cp-course-meta">' + total + ' lessons \u00b7 ' + totalDuration + '</div>'
            + '</div>'
            + '</div>'
            + '<button class="cp-close-btn" id="cpCloseBtn" title="Collapse sidebar">\u00d7</button>'
            + '</div>'
            + '</div>';

        // Segmented progress bar
        html += '<div class="cp-seg-bar">';
        for (var sgi = 0; sgi < sections.length; sgi++) {
            var sgSec = sections[sgi];
            var sgCompleted = g(sgSec, 'CompletedCount') || 0;
            var sgTotal = g(sgSec, 'TotalCount') || 0;
            var sgClass = 'pending';
            if (sgTotal > 0 && sgCompleted >= sgTotal) {
                sgClass = 'done';
            } else if (sgCompleted > 0) {
                sgClass = 'active';
            }
            html += '<div class="cp-seg ' + sgClass + '" style="flex:' + sgTotal + ';"></div>';
        }
        html += '</div>';

        // Continue button
        if (completed >= total && total > 0) {
            html += '<div class="cp-continue-btn" id="cpContinueBtn">\u25b6 Replay Course</div>';
        } else {
            html += '<div class="cp-continue-btn" id="cpContinueBtn">\u25b6 Continue \u2014 ' + esc(nextLessonName) + '</div>';
        }

        // Scrollable section list
        var foundNext = false;
        html += '<div class="cp-sections">';

        for (var i = 0; i < sections.length; i++) {
            var sec = sections[i];
            var sName = g(sec, 'Name') || '';
            var sLess = g(sec, 'Lessons') || [];
            var sCompleted = g(sec, 'CompletedCount') || 0;
            var sTotal = g(sec, 'TotalCount') || 0;
            var secDone = sTotal > 0 && sCompleted >= sTotal;
            var secActive = !secDone && sCompleted > 0;

            var secClass = 'cp-section';
            if (secDone) secClass += ' cp-sec-complete';
            else if (secActive) secClass += ' cp-sec-active';

            var dotColor = '#666';
            var dotChar = '\u25cb';
            if (secDone) {
                dotColor = '#4caf50';
                dotChar = '\u25cf';
            } else if (secActive) {
                dotColor = '#00a4dc';
                dotChar = '\u25cf';
            }

            html += '<div class="' + secClass + '">'
                + '<div class="cp-section-hdr" data-cpidx="' + i + '">'
                + '<span class="cp-section-dot" style="color:' + dotColor + ';">' + dotChar + '</span>'
                + '<span class="cp-section-name' + (secDone ? ' cp-struck' : '') + '">' + esc(sName) + '</span>';
            if (secDone) {
                html += '<span class="cp-section-badge">DONE</span>';
            }
            html += '<span class="cp-section-count">' + sCompleted + '/' + sTotal + '</span>'
                + '<button class="cp-section-play" data-cp-section="' + i + '" title="Play section">\u25b6</button>'
                + '<span class="cp-section-arrow">\u25b6</span>'
                + '</div>';

            html += '<div class="cp-lessons" data-cpidx="' + i + '">';

            for (var j = 0; j < sLess.length; j++) {
                var l = sLess[j];
                var lId = g(l, 'Id');
                var lName = g(l, 'Name') || '';
                var played = g(l, 'Played');
                var rt = g(l, 'RunTimeTicks') || 0;
                var isNext = !played && !foundNext;
                if (isNext) foundNext = true;

                var statusClass = played ? 'played' : 'unplayed';
                var statusIcon = played ? '&#10003;' : (isNext ? '&#9656;' : '&#9675;');
                var statusTitle = played ? 'Mark unwatched' : 'Mark watched';

                html += '<div class="cp-lesson' + (isNext ? ' cp-lesson-next' : '') + '">'
                    + '<div class="cp-lesson-status ' + statusClass
                    + '" data-cp-toggle="' + lId + '" data-cp-played="' + (played ? '1' : '0')
                    + '" title="' + statusTitle + '">'
                    + statusIcon + '</div>'
                    + '<div class="cp-lesson-name"><a href="#!/details?id=' + lId + '">' + esc(lName) + '</a></div>';
                if (isNext) {
                    html += '<span class="cp-next-badge">NEXT</span>';
                }
                html += '<div class="cp-lesson-dur">' + formatDuration(rt) + '</div>'
                    + '</div>';
            }

            html += '</div></div>';
        }

        html += '</div>';

        // Resize handle
        html += '<div class="cp-resize-handle" id="cpResizeHandle"></div>';

        sidebarEl.innerHTML = html;

        // --- Wire up event listeners ---

        // 1. Close button
        var closeBtn = document.getElementById('cpCloseBtn');
        if (closeBtn) {
            closeBtn.addEventListener('click', function () {
                collapseSidebar();
            });
        }

        // 2. Continue button
        var continueBtn = document.getElementById('cpContinueBtn');
        if (continueBtn) {
            continueBtn.addEventListener('click', function () {
                var ids = (completed >= total && total > 0)
                    ? getAllLessonIds(sections)
                    : getLessonIdsFromContinue(sections);
                playItems(ids);
            });
        }

        // 3. Section headers — toggle open/close
        sidebarEl.querySelectorAll('.cp-section-hdr').forEach(function (hdr) {
            hdr.addEventListener('click', function (e) {
                if (e.target.closest('.cp-section-play')) return;
                var idx = this.getAttribute('data-cpidx');
                var lessonsDiv = sidebarEl.querySelector('.cp-lessons[data-cpidx="' + idx + '"]');
                if (lessonsDiv) lessonsDiv.classList.toggle('open');
                this.classList.toggle('open');
            });
        });

        // 4. Section play buttons
        sidebarEl.querySelectorAll('.cp-section-play').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.stopPropagation();
                var idx = parseInt(this.getAttribute('data-cp-section'), 10);
                var sec = sections[idx];
                if (!sec) return;
                var lessons = g(sec, 'Lessons') || [];
                var ids = [];
                for (var k = 0; k < lessons.length; k++) {
                    ids.push(g(lessons[k], 'Id'));
                }
                playItems(ids);
            });
        });

        // 5. Toggle played status
        sidebarEl.querySelectorAll('.cp-lesson-status[data-cp-toggle]').forEach(function (el) {
            el.addEventListener('click', function (e) {
                e.stopPropagation();
                togglePlayed(this, sidebarEl, sections);
            });
        });

        // 6. Auto-open first uncompleted section
        for (var ao = 0; ao < sections.length; ao++) {
            if ((g(sections[ao], 'CompletedCount') || 0) < (g(sections[ao], 'TotalCount') || 0)) {
                var autoHdr = sidebarEl.querySelector('.cp-section-hdr[data-cpidx="' + ao + '"]');
                if (autoHdr) autoHdr.click();
                break;
            }
        }

        // --- Resize handle logic ---
        var resizeHandle = document.getElementById('cpResizeHandle');
        if (resizeHandle) {
            var startX, startWidth;
            function onMouseMove(e) {
                var newWidth = startWidth + (e.clientX - startX);
                newWidth = Math.max(MIN_WIDTH, Math.min(MAX_WIDTH, newWidth));
                sidebarEl.style.width = newWidth + 'px';
                pushContent(newWidth);
            }
            function onMouseUp() {
                document.removeEventListener('mousemove', onMouseMove);
                document.removeEventListener('mouseup', onMouseUp);
                document.body.style.cursor = '';
                document.body.style.userSelect = '';
                var finalWidth = parseInt(sidebarEl.style.width, 10);
                if (finalWidth >= MIN_WIDTH && finalWidth <= MAX_WIDTH) {
                    setSidebarWidth(finalWidth);
                }
            }
            resizeHandle.addEventListener('mousedown', function (e) {
                e.preventDefault();
                startX = e.clientX;
                startWidth = parseInt(sidebarEl.style.width, 10) || getSidebarWidth();
                document.body.style.cursor = 'col-resize';
                document.body.style.userSelect = 'none';
                document.addEventListener('mousemove', onMouseMove);
                document.addEventListener('mouseup', onMouseUp);
            });
        }
    }


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

    function apiFetchRaw(path, method) {
        var auth = getAuth();
        if (!auth) return Promise.reject(new Error('Not authenticated'));
        return fetch(window.location.origin + path, {
            method: method || 'GET',
            headers: { 'Authorization': 'MediaBrowser Token="' + auth.token + '"' }
        });
    }

    function togglePlayed(statusEl, container, sections) {
        var auth = getAuth();
        if (!auth) return;
        var lessonId = statusEl.getAttribute('data-cp-toggle');
        var isPlayed = statusEl.getAttribute('data-cp-played') === '1';
        var newPlayed = !isPlayed;

        // Optimistic DOM update — icon.
        statusEl.setAttribute('data-cp-played', newPlayed ? '1' : '0');
        statusEl.className = 'cp-lesson-status ' + (newPlayed ? 'played' : 'unplayed');
        statusEl.title = newPlayed ? 'Mark unwatched' : 'Mark watched';
        statusEl.innerHTML = newPlayed ? '&#10003;' : '&#9675;';

        // Update section count text.
        var sectionEl = statusEl.closest('.cp-section');
        if (sectionEl) {
            var countEl = sectionEl.querySelector('.cp-section-count');
            var statuses = sectionEl.querySelectorAll('.cp-lesson-status[data-cp-toggle]');
            var sPlayed = 0;
            statuses.forEach(function (s) { if (s.getAttribute('data-cp-played') === '1') sPlayed++; });
            var sTotal = statuses.length;
            if (countEl) countEl.textContent = sPlayed + '/' + sTotal;
            // Update section progress bar.
            var pfill = sectionEl.querySelector('.cp-section-pfill');
            if (pfill) {
                var sPct = sTotal > 0 ? Math.round(sPlayed * 100 / sTotal) : 0;
                pfill.style.width = sPct + '%';
                pfill.className = 'cp-section-pfill ' + (sPlayed >= sTotal && sTotal > 0 ? 'cp-sec-done' : 'cp-sec-active');
            }
        }

        // Update overall progress.
        var allStatuses = container.querySelectorAll('.cp-lesson-status[data-cp-toggle]');
        var totalPlayed = 0;
        allStatuses.forEach(function (s) { if (s.getAttribute('data-cp-played') === '1') totalPlayed++; });
        var totalAll = allStatuses.length;
        var pctEl = container.querySelector('.cp-progress-text');
        if (pctEl) pctEl.textContent = totalPlayed + ' / ' + totalAll + ' lessons';
        var fillEl = container.querySelector('.cp-progress-fill');
        if (fillEl) {
            var newPct = totalAll > 0 ? Math.round(totalPlayed * 100 / totalAll) : 0;
            fillEl.style.width = newPct + '%';
            if (newPct >= 100) { fillEl.classList.add('cp-complete'); } else { fillEl.classList.remove('cp-complete'); }
        }

        // Update segmented progress bar
        var segs = container.querySelectorAll('.cp-seg');
        var sectionEls = container.querySelectorAll('.cp-section');
        sectionEls.forEach(function(secEl, idx) {
            var statuses = secEl.querySelectorAll('.cp-lesson-status[data-cp-toggle]');
            var sPlayed = 0;
            statuses.forEach(function(s) { if (s.getAttribute('data-cp-played') === '1') sPlayed++; });
            var sTotal = statuses.length;
            if (segs[idx]) {
                segs[idx].className = 'cp-seg ' + (sPlayed >= sTotal && sTotal > 0 ? 'done' : sPlayed > 0 ? 'active' : 'pending');
            }
        });

        // Fire API in background.
        var method = isPlayed ? 'DELETE' : 'POST';
        apiFetchRaw('/Users/' + auth.userId + '/PlayedItems/' + lessonId, method)
            .catch(function () {
                // Revert on failure.
                statusEl.setAttribute('data-cp-played', isPlayed ? '1' : '0');
                statusEl.className = 'cp-lesson-status ' + (isPlayed ? 'played' : 'unplayed');
                statusEl.title = isPlayed ? 'Mark unwatched' : 'Mark watched';
                statusEl.innerHTML = isPlayed ? '&#10003;' : '&#9675;';
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

    function injectStyles() {
        if (document.getElementById('cp-sidebar-styles')) return;
        var style = document.createElement('style');
        style.id = 'cp-sidebar-styles';
        style.textContent = ''
            // Sidebar shell
            + '.cp-sidebar { position: fixed; left: 0; bottom: 0; z-index: 999; background: #111114; border-right: 1px solid #222; display: flex; flex-direction: column; transition: width 250ms ease; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; }'
            + '.cp-sidebar.cp-collapsed { width: 28px; cursor: pointer; overflow: hidden; }'
            + '.cp-sidebar.cp-hidden { display: none; }'
            // Tab handle (collapsed)
            + '.cp-tab-label { writing-mode: vertical-rl; transform: rotate(180deg); color: #666; font-size: 10px; letter-spacing: 1px; text-transform: uppercase; }'
            + '.cp-tab-arrow { color: #00a4dc; font-size: 12px; margin-top: 8px; }'
            + '.cp-tab-progress { width: 4px; height: 60px; background: #222; border-radius: 2px; overflow: hidden; }'
            + '.cp-tab-pfill { width: 100%; background: #00a4dc; border-radius: 2px; transition: height 0.3s; }'
            // Header (expanded)
            + '.cp-header { padding: 14px; background: linear-gradient(180deg, rgba(0,164,220,0.12) 0%, transparent 100%); border-bottom: 1px solid rgba(255,255,255,0.05); }'
            + '.cp-course-icon { width: 28px; height: 28px; background: #00a4dc; border-radius: 5px; color: #fff; font-size: 13px; font-weight: 700; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }'
            + '.cp-course-name { color: #eee; font-size: 0.9em; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }'
            + '.cp-course-meta { color: #666; font-size: 0.75em; }'
            + '.cp-close-btn { background: none; border: none; color: #555; cursor: pointer; font-size: 18px; padding: 0; line-height: 1; }'
            + '.cp-close-btn:hover { color: #999; }'
            // Segmented progress
            + '.cp-seg-bar { display: flex; gap: 3px; height: 6px; margin: 10px 14px 0; }'
            + '.cp-seg { border-radius: 2px; transition: background 0.3s; }'
            + '.cp-seg.done { background: #4caf50; }'
            + '.cp-seg.active { background: #00a4dc; }'
            + '.cp-seg.pending { background: rgba(255,255,255,0.08); }'
            // Continue button
            + '.cp-continue-btn { margin: 10px 14px; background: linear-gradient(135deg, #00a4dc, #0090c4); color: #fff; border: none; padding: 10px; border-radius: 6px; font-size: 0.85em; font-weight: 600; cursor: pointer; text-align: center; transition: opacity 0.2s; }'
            + '.cp-continue-btn:hover { opacity: 0.9; }'
            // Scrollable section list
            + '.cp-sections { flex: 1; overflow-y: auto; overflow-x: hidden; padding: 0 10px 10px; }'
            + '.cp-section { border-radius: 5px; margin-bottom: 4px; }'
            + '.cp-section.cp-sec-complete { opacity: 0.6; }'
            + '.cp-section.cp-sec-active { background: rgba(0,164,220,0.06); border: 1px solid rgba(0,164,220,0.15); }'
            + '.cp-section-hdr { padding: 8px; cursor: pointer; display: flex; align-items: center; gap: 6px; border-radius: 5px; }'
            + '.cp-section-hdr:hover { background: rgba(255,255,255,0.04); }'
            + '.cp-section-dot { font-size: 10px; flex-shrink: 0; }'
            + '.cp-section-name { flex: 1; font-size: 0.85em; color: #aaa; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }'
            + '.cp-section-name.cp-struck { text-decoration: line-through; }'
            + '.cp-section-count { font-size: 0.75em; color: #555; flex-shrink: 0; }'
            + '.cp-section-badge { font-size: 0.65em; color: #4caf50; flex-shrink: 0; }'
            + '.cp-section-play { background: none; border: 1px solid #444; color: #999; width: 22px; height: 22px; border-radius: 50%; cursor: pointer; display: flex; align-items: center; justify-content: center; font-size: 0.55em; transition: all 0.2s; flex-shrink: 0; padding: 0; }'
            + '.cp-section-play:hover { border-color: #00a4dc; color: #00a4dc; background: rgba(0,164,220,0.1); }'
            + '.cp-section-arrow { color: #666; transition: transform 200ms; font-size: 0.7em; flex-shrink: 0; }'
            + '.cp-section-hdr.open .cp-section-arrow { transform: rotate(90deg); }'
            // Lessons
            + '.cp-lessons { max-height: 0; overflow: hidden; transition: max-height 200ms ease-out; padding-left: 20px; border-left: 1px solid rgba(0,164,220,0.2); margin-left: 12px; }'
            + '.cp-lessons.open { max-height: 5000px; }'
            + '.cp-lesson { display: flex; align-items: center; padding: 4px 0; gap: 6px; border-radius: 3px; }'
            + '.cp-lesson:hover { background: rgba(255,255,255,0.04); }'
            + '.cp-lesson.cp-playing { background: rgba(0,164,220,0.1); padding: 4px 6px; margin: 0 -6px; }'
            + '.cp-lesson.cp-lesson-next { background: rgba(0,164,220,0.08); border-left: 2px solid #00a4dc; padding-left: 4px; margin-left: -2px; }'
            + '.cp-lesson-status { width: 14px; text-align: center; cursor: pointer; flex-shrink: 0; font-size: 0.75em; }'
            + '.cp-lesson-status.played { color: #4caf50; }'
            + '.cp-lesson-status.unplayed { color: #444; }'
            + '.cp-lesson-name { flex: 1; min-width: 0; }'
            + '.cp-lesson-name a { color: #888; text-decoration: none; font-size: 0.8em; }'
            + '.cp-lesson-name a:hover { color: #00a4dc; }'
            + '.cp-next-badge { background: #00a4dc; color: #fff; font-size: 0.55em; padding: 1px 4px; border-radius: 2px; flex-shrink: 0; }'
            + '.cp-lesson-dur { color: #555; font-size: 0.7em; flex-shrink: 0; }'
            // Resize handle
            + '.cp-resize-handle { position: absolute; top: 0; right: -3px; width: 6px; height: 100%; cursor: col-resize; z-index: 1000; }'
            + '.cp-resize-handle::after { content: ""; position: absolute; top: 50%; left: 50%; transform: translate(-50%,-50%); width: 4px; height: 40px; background: rgba(255,255,255,0.1); border-radius: 2px; opacity: 0; transition: opacity 150ms; }'
            + '.cp-resize-handle:hover::after { opacity: 1; }'
            // Content push
            + '.cp-content-push { transition: margin-left 250ms ease; }'
            // Responsive
            + '@media (max-width: 767px) { .cp-sidebar.cp-expanded { position: fixed; width: 85vw !important; max-width: 360px; box-shadow: 4px 0 20px rgba(0,0,0,0.5); } }'
            + '.cp-backdrop { position: fixed; inset: 0; background: rgba(0,0,0,0.5); z-index: 998; display: none; }';
        document.head.appendChild(style);
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
        var itemPath = (item.Path || '').replace(/\/+$/, '');
        if (!itemPath || !COURSE_PATHS || !COURSE_PATHS.length) return false;
        for (var i = 0; i < COURSE_PATHS.length; i++) {
            var cp = (COURSE_PATHS[i] || '').replace(/\/+$/, '');
            if (itemPath.indexOf(cp) === 0 && itemPath !== cp) return true;
        }
        return false;
    }

    var lessonCourseCache = {};

    function findCourseIdForItem(item) {
        if ((item.IsFolder || item.isFolder) && isCourseItem(item)) {
            return Promise.resolve(item.Id || item.id);
        }
        var itemId = item.Id || item.id;
        if (lessonCourseCache[itemId]) {
            return Promise.resolve(lessonCourseCache[itemId]);
        }
        function walkUp(id) {
            return apiFetch('/Items/' + id + '?Fields=Path,ParentId').then(function(parent) {
                if (!parent) return null;
                var parentPath = (parent.Path || '').replace(/\/+$/, '');
                if (!parentPath) return null;
                if ((parent.IsFolder || parent.isFolder) && isCourseItem(parent)) {
                    var parentParentId = parent.ParentId || parent.parentId;
                    if (!parentParentId) {
                        lessonCourseCache[itemId] = parent.Id || parent.id;
                        return parent.Id || parent.id;
                    }
                    return apiFetch('/Items/' + parentParentId + '?Fields=Path').then(function(gp) {
                        if (!gp) {
                            lessonCourseCache[itemId] = parent.Id || parent.id;
                            return parent.Id || parent.id;
                        }
                        var gpPath = (gp.Path || '').replace(/\/+$/, '');
                        if (COURSE_PATHS && COURSE_PATHS.length) {
                            for (var i = 0; i < COURSE_PATHS.length; i++) {
                                var cp = (COURSE_PATHS[i] || '').replace(/\/+$/, '');
                                if (gpPath === cp) {
                                    lessonCourseCache[itemId] = parent.Id || parent.id;
                                    return parent.Id || parent.id;
                                }
                            }
                        }
                        return walkUp(parent.Id || parent.id);
                    });
                }
                return null;
            });
        }
        var parentId = item.ParentId || item.parentId;
        if (!parentId) return Promise.resolve(null);
        return walkUp(parentId);
    }

    function init() {
        injectStyles();
        createSidebar();
        var lastUrl = '';
        invalidateOverview = function() { lastUrl = ''; };

        function onPageChange() {
            var url = window.location.hash;
            if (url === lastUrl) return;
            lastUrl = url;
            var itemId = getItemIdFromUrl();
            if (!itemId || !COURSE_PATHS || !COURSE_PATHS.length) {
                hideSidebar();
                return;
            }
            apiFetch('/Items/' + itemId + '?Fields=Path,ParentId').then(function(item) {
                if (!item) { hideSidebar(); return; }
                findCourseIdForItem(item).then(function(courseId) {
                    if (!courseId) { hideSidebar(); return; }
                    var auth = getAuth();
                    if (!auth) { hideSidebar(); return; }
                    apiFetch('/Courses/' + courseId + '/Structure?userId=' + auth.userId + '&_t=' + Date.now())
                        .then(function(data) {
                            if (!data) { hideSidebar(); return; }
                            if (getSidebarOpen()) {
                                expandSidebar();
                                renderSidebarContent(data, courseId);
                            } else {
                                lastCourseData = data;
                                lastCourseId = courseId;
                                lastProgressPct = g(data, 'ProgressPercent') || 0;
                                collapseSidebar();
                            }
                        })
                        .catch(function() { hideSidebar(); });
                }).catch(function() { hideSidebar(); });
            }).catch(function() { hideSidebar(); });
        }

        document.addEventListener('visibilitychange', function() {
            if (!document.hidden) onPageChange();
        });
        window.addEventListener('hashchange', onPageChange);
        window.addEventListener('popstate', onPageChange);
        var origPush = history.pushState;
        var origReplace = history.replaceState;
        history.pushState = function() {
            origPush.apply(this, arguments);
            onPageChange();
        };
        history.replaceState = function() {
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
