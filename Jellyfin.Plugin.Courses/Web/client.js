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

    // Toggle a collapsible panel using scrollHeight for accurate animation.
    function toggleCollapsible(el) {
        if (!el) return;
        if (el.classList.contains('open')) {
            // Collapse: set max-height to current scrollHeight, then force reflow, then set to 0
            el.style.maxHeight = el.scrollHeight + 'px';
            // Force reflow so the browser registers the starting value
            el.offsetHeight; // eslint-disable-line no-unused-expressions
            el.style.maxHeight = '0';
            el.classList.remove('open');
        } else {
            el.classList.add('open');
            el.style.maxHeight = el.scrollHeight + 'px';
            // After transition, remove max-height so dynamically added content isn't clipped
            var onEnd = function () {
                el.removeEventListener('transitionend', onEnd);
                if (el.classList.contains('open')) {
                    el.style.maxHeight = 'none';
                }
            };
            el.addEventListener('transitionend', onEnd);
        }
    }

    // Module-level sidebar state
    var sidebarEl = null;
    var backdropEl = null;
    var lastCourseData = null;
    var lastCourseId = null;
    var lastProgressPct = 0;
    var resourceCache = {};

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
        if (sidebarEl._onTabClick) {
            sidebarEl.removeEventListener('click', sidebarEl._onTabClick);
        }
        sidebarEl.className = 'cp-sidebar cp-collapsed';
        sidebarEl.style.width = '';
        sidebarEl.style.top = getHeaderHeight() + 'px';
        setSidebarOpen(false);
        backdropEl.style.display = 'none';
        if (window.innerWidth < 768) {
            pushContent(0);
        } else {
            pushContent(28);
        }
        sidebarEl.innerHTML = renderCollapsedTab(lastProgressPct);
        sidebarEl._onTabClick = function() {
            sidebarEl.removeEventListener('click', sidebarEl._onTabClick);
            sidebarEl._onTabClick = null;
            expandSidebar();
            renderSidebarContent(lastCourseData, lastCourseId);
        };
        sidebarEl.addEventListener('click', sidebarEl._onTabClick);
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

        // Clear resource cache when switching courses
        if (courseId !== lastCourseId) {
            resourceCache = {};
        }

        lastCourseData = data;
        lastCourseId = courseId;

        var sections = g(data, 'Sections') || [];
        var total = g(data, 'TotalLessons') || 0;
        var completed = g(data, 'CompletedLessons') || 0;
        var pct = g(data, 'ProgressPercent') || 0;
        var courseName = g(data, 'Name') || 'Course';

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
            + '<div style="display:flex;align-items:center;justify-content:space-between;gap:8px;">'
            + '<div style="display:flex;align-items:center;gap:8px;min-width:0;flex:1;">'
            + '<div class="cp-course-icon">' + esc(initial) + '</div>'
            + '<div style="min-width:0;flex:1;">'
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
                    + '<div class="cp-lesson-name"><a href="#" data-cp-play="' + lId + '" data-cp-section="' + i + '">' + esc(lName) + '</a></div>';
                if (isNext) {
                    html += '<span class="cp-next-badge">NEXT</span>';
                }
                html += '<div class="cp-lesson-dur">' + formatDuration(rt) + '</div>'
                    + '</div>';
            }

            // Lazy resource placeholder (loaded on section expand)
            html += '<div class="cp-sec-resources-lazy" data-cp-section-id="' + g(sec, 'Id') + '"></div>';

            html += '</div>'; // close .cp-lessons
            html += '</div>'; // close .cp-section
        }

        // Course-level lazy resource placeholder
        html += '<div class="cp-course-resources-lazy" data-cp-course-id="' + courseId + '"></div>';

        html += '</div>';

        // Resize handle
        html += '<div class="cp-resize-handle" id="cpResizeHandle"></div>';

        sidebarEl.innerHTML = html;

        // --- Wire up event listeners ---

        // 1. Close button
        var closeBtn = document.getElementById('cpCloseBtn');
        if (closeBtn) {
            closeBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                collapseSidebar();
            });
        }

        // 2. Continue button
        var continueBtn = document.getElementById('cpContinueBtn');
        if (continueBtn) {
            continueBtn.addEventListener('click', function (e) {
                e.stopPropagation();
                var allIds = getAllLessonIds(sections);
                var startIdx = (completed >= total && total > 0)
                    ? 0
                    : getContinueIndex(sections);
                playItems(allIds, startIdx);
            });
        }

        // 3. Section headers — toggle open/close + lazy resource load
        sidebarEl.querySelectorAll('.cp-section-hdr').forEach(function (hdr) {
            hdr.addEventListener('click', function (e) {
                if (e.target.closest('.cp-section-play')) return;
                var idx = this.getAttribute('data-cpidx');
                var lessonsDiv = sidebarEl.querySelector('.cp-lessons[data-cpidx="' + idx + '"]');
                if (lessonsDiv) {
                    toggleCollapsible(lessonsDiv);
                    // Lazy-load resources on first expand
                    if (lessonsDiv.classList.contains('open')) {
                        var placeholder = lessonsDiv.querySelector('.cp-sec-resources-lazy');
                        if (placeholder) {
                            var secId = placeholder.getAttribute('data-cp-section-id');
                            loadResources(placeholder, secId, courseId);
                        }
                    }
                }
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

        // 6. Lesson name click — play from that lesson through end of section
        sidebarEl.querySelectorAll('a[data-cp-play]').forEach(function (el) {
            el.addEventListener('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                var lessonId = this.getAttribute('data-cp-play');
                var secIdx = parseInt(this.getAttribute('data-cp-section'), 10);
                var sec = sections[secIdx];
                if (!sec) return;
                var lessons = g(sec, 'Lessons') || [];
                var allIds = [];
                var startIndex = 0;
                for (var k = 0; k < lessons.length; k++) {
                    if (g(lessons[k], 'Id') === lessonId) startIndex = k;
                    allIds.push(g(lessons[k], 'Id'));
                }
                playItems(allIds, startIndex);
            });
        });

        // 7. Auto-open first uncompleted section
        for (var ao = 0; ao < sections.length; ao++) {
            if ((g(sections[ao], 'CompletedCount') || 0) < (g(sections[ao], 'TotalCount') || 0)) {
                var autoHdr = sidebarEl.querySelector('.cp-section-hdr[data-cpidx="' + ao + '"]');
                if (autoHdr) autoHdr.click();
                break;
            }
        }

        // 8. Lazy-load resources when section is expanded
        function loadResources(container, sectionId, cid) {
            if (resourceCache[sectionId] === 'loading') return;
            if (resourceCache[sectionId]) {
                renderCachedResources(container, resourceCache[sectionId], sectionId, cid);
                return;
            }
            resourceCache[sectionId] = 'loading';
            container.innerHTML = '<div class="cp-res-loading" style="color:#555;font-size:0.8em;padding:6px 8px;"><i class="fa-solid fa-spinner fa-spin"></i> Loading resources\u2026</div>';
            var url = '/Courses/' + cid + '/ResourceScan' + (sectionId !== cid ? '?sectionId=' + sectionId : '');
            apiFetch(url).then(function (res) {
                resourceCache[sectionId] = res;
                renderCachedResources(container, res, sectionId, cid);
            }).catch(function () {
                container.innerHTML = '';
                resourceCache[sectionId] = null;
            });
        }

        function renderCachedResources(container, res, sectionId, cid) {
            var files = g(res, 'Files') || [];
            var folders = g(res, 'Folders') || [];
            if (files.length === 0 && folders.length === 0) {
                container.innerHTML = '';
                return;
            }
            var fileCount = countResourceTree(files, folders);
            var isCourse = sectionId === cid;
            var hdrClass = isCourse ? 'cp-sec-res-hdr cp-course-res-hdr' : 'cp-sec-res-hdr';
            var bodyClass = isCourse ? 'cp-res-root-body' : 'cp-res-files';
            var html = '<div class="' + (isCourse ? 'cp-resource-root' : 'cp-sec-resources') + '">'
                + '<div class="' + hdrClass + '">'
                + '<span class="cp-res-icon"><i class="fa-solid fa-paperclip"></i></span>'
                + '<span class="cp-sec-res-label">Resources</span>'
                + '<span class="cp-section-count">' + fileCount + ' files</span>'
                + '<span class="cp-section-arrow">\u25b6</span>'
                + '</div>'
                + '<div class="' + bodyClass + '">'
                + renderResourceFiles(files)
                + renderResourceFolders(folders)
                + '</div></div>';
            container.innerHTML = html;
            wireResourceEvents(container, cid);
        }

        function wireResourceEvents(root, cid) {
            // Resource headers toggle
            root.querySelectorAll('.cp-sec-res-hdr').forEach(function (hdr) {
                hdr.addEventListener('click', function () {
                    var body = this.nextElementSibling;
                    toggleCollapsible(body);
                    this.classList.toggle('open');
                });
            });
            // Folder headers toggle
            root.querySelectorAll('.cp-res-folder-hdr').forEach(function (hdr) {
                hdr.addEventListener('click', function (e) {
                    if (e.target.closest('.cp-res-dl-all')) return;
                    var body = this.nextElementSibling;
                    toggleCollapsible(body);
                    this.classList.toggle('open');
                });
            });
            // Download zip
            root.querySelectorAll('.cp-res-dl-all').forEach(function (btn) {
                btn.addEventListener('click', function (e) {
                    e.stopPropagation();
                    var zipPath = this.getAttribute('data-cp-zip-path');
                    window.open(resourceUrl(cid, zipPath, '&zip=true'));
                });
            });
            // File click → preview
            root.querySelectorAll('.cp-res-file').forEach(function (el) {
                el.addEventListener('click', function () {
                    var resPath = this.getAttribute('data-cp-res-path');
                    var resExt = this.getAttribute('data-cp-res-ext');
                    var resName = this.getAttribute('data-cp-res-name');
                    openResourcePreview(cid, resPath, resExt, resName);
                });
            });
        }

        // 9. Lazy-load course-level resources after render
        var coursePlaceholder = sidebarEl.querySelector('.cp-course-resources-lazy');
        if (coursePlaceholder) {
            loadResources(coursePlaceholder, courseId, courseId);
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

    function resourceUrl(courseId, path, extra) {
        var auth = getAuth();
        var url = '/Courses/' + courseId + '/Resources?path=' + encodeURIComponent(path);
        if (extra) url += extra;
        if (auth) url += '&api_key=' + auth.token;
        return url;
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

    function getFileIcon(ext, name) {
        ext = (ext || '').toLowerCase();
        name = (name || '').toLowerCase();
        // Filename-based icons for extensionless files
        var nameMap = {
            'dockerfile': 'devicon-docker-plain colored',
            'jenkinsfile': 'devicon-jenkins-plain colored',
            'vagrantfile': 'devicon-vagrant-plain colored',
            'makefile': 'devicon-cmake-plain colored',
            'gnumakefile': 'devicon-cmake-plain colored',
            'gemfile': 'devicon-ruby-plain colored',
            'rakefile': 'devicon-ruby-plain colored'
        };
        if (!ext && nameMap[name]) return '<i class="' + nameMap[name] + ' cp-icon"></i>';
        // Also match Dockerfile.* variants
        if (!ext && (name === 'dockerfile' || name.indexOf('dockerfile.') === 0)) return '<i class="devicon-docker-plain colored cp-icon"></i>';
        // Devicon language-specific icons — 'colored' class provides brand colors
        var deviconMap = {
            // JavaScript / TypeScript
            '.js': 'devicon-javascript-plain colored',
            '.mjs': 'devicon-javascript-plain colored',
            '.cjs': 'devicon-javascript-plain colored',
            '.jsx': 'devicon-react-original colored',
            '.ts': 'devicon-typescript-plain colored',
            '.mts': 'devicon-typescript-plain colored',
            '.cts': 'devicon-typescript-plain colored',
            '.tsx': 'devicon-react-original colored',
            // Web
            '.html': 'devicon-html5-plain colored',
            '.css': 'devicon-css3-plain colored',
            '.scss': 'devicon-sass-original colored',
            '.sass': 'devicon-sass-original colored',
            '.less': 'devicon-less-plain-wordmark colored',
            '.vue': 'devicon-vuejs-plain colored',
            '.svelte': 'devicon-svelte-plain colored',
            // Systems / Backend
            '.py': 'devicon-python-plain colored',
            '.java': 'devicon-java-plain colored',
            '.cs': 'devicon-csharp-plain colored',
            '.go': 'devicon-go-plain colored',
            '.rs': 'devicon-rust-original colored',
            '.rb': 'devicon-ruby-plain colored',
            '.php': 'devicon-php-plain colored',
            '.swift': 'devicon-swift-plain colored',
            '.kt': 'devicon-kotlin-plain colored',
            '.kts': 'devicon-kotlin-plain colored',
            '.scala': 'devicon-scala-plain colored',
            '.lua': 'devicon-lua-plain colored',
            '.pl': 'devicon-perl-plain colored',
            '.pm': 'devicon-perl-plain colored',
            '.r': 'devicon-r-plain colored',
            '.dart': 'devicon-dart-plain colored',
            '.c': 'devicon-c-plain colored',
            '.cpp': 'devicon-cplusplus-plain colored',
            '.h': 'devicon-c-plain colored',
            '.hpp': 'devicon-cplusplus-plain colored',
            // Data / Config
            '.json': 'devicon-json-plain colored',
            '.xml': 'devicon-xml-plain colored',
            '.yml': 'devicon-yaml-plain colored',
            '.yaml': 'devicon-yaml-plain colored',
            '.toml': 'devicon-toml-plain colored',
            '.graphql': 'devicon-graphql-plain colored',
            '.gql': 'devicon-graphql-plain colored',
            // Shell / Scripting
            '.sh': 'devicon-bash-plain colored',
            '.bash': 'devicon-bash-plain colored',
            '.zsh': 'devicon-bash-plain colored',
            '.ps1': 'devicon-powershell-plain colored',
            '.psm1': 'devicon-powershell-plain colored',
            // DevOps / IaC
            '.tf': 'devicon-terraform-plain colored',
            '.tfvars': 'devicon-terraform-plain colored',
            '.hcl': 'devicon-terraform-plain colored',
            '.gradle': 'devicon-gradle-plain colored',
            // Database
            '.sql': 'devicon-azuresqldatabase-plain colored',
            // Docs
            '.md': 'devicon-markdown-original colored',
            '.markdown': 'devicon-markdown-original colored'
        };
        if (deviconMap[ext]) return '<i class="' + deviconMap[ext] + ' cp-icon"></i>';
        // Font Awesome for general file types
        if (ext === '.pdf') return '<i class="fa-solid fa-file-pdf cp-icon" style="color:#e74c3c;"></i>';
        if (['.png','.jpg','.jpeg','.gif','.svg','.webp','.bmp','.ico'].indexOf(ext) >= 0) return '<i class="fa-solid fa-file-image cp-icon" style="color:#3498db;"></i>';
        if (['.zip','.tar','.gz','.rar','.7z','.bz2','.xz','.tgz'].indexOf(ext) >= 0) return '<i class="fa-solid fa-file-zipper cp-icon" style="color:#f39c12;"></i>';
        if (['.txt','.log','.cfg','.conf','.ini','.env','.properties'].indexOf(ext) >= 0) return '<i class="fa-solid fa-file-lines cp-icon" style="color:#95a5a6;"></i>';
        if (['.csv','.tsv'].indexOf(ext) >= 0) return '<i class="fa-solid fa-file-csv cp-icon" style="color:#27ae60;"></i>';
        if (['.doc','.docx','.odt','.rtf'].indexOf(ext) >= 0) return '<i class="fa-solid fa-file-word cp-icon" style="color:#2b579a;"></i>';
        if (['.xls','.xlsx','.ods'].indexOf(ext) >= 0) return '<i class="fa-solid fa-file-excel cp-icon" style="color:#217346;"></i>';
        if (['.ppt','.pptx','.odp'].indexOf(ext) >= 0) return '<i class="fa-solid fa-file-powerpoint cp-icon" style="color:#d24726;"></i>';
        if (['.bat','.cmd'].indexOf(ext) >= 0) return '<i class="fa-solid fa-terminal cp-icon" style="color:#95a5a6;"></i>';
        if (ext === '.proto') return '<i class="fa-solid fa-diagram-project cp-icon" style="color:#95a5a6;"></i>';
        return '<i class="fa-solid fa-file cp-icon" style="color:#95a5a6;"></i>';
    }

    function formatSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1048576) return (bytes / 1024).toFixed(1) + ' KB';
        if (bytes < 1073741824) return (bytes / 1048576).toFixed(1) + ' MB';
        return (bytes / 1073741824).toFixed(1) + ' GB';
    }

    function renderResourceFiles(files) {
        var html = '';
        for (var i = 0; i < files.length; i++) {
            var f = files[i];
            var fName = g(f, 'Name') || '';
            var fPath = g(f, 'RelativePath') || '';
            var fExt = g(f, 'Extension') || '';
            var fSize = g(f, 'Size') || 0;
            html += '<div class="cp-res-file" data-cp-res-path="' + esc(fPath) + '" data-cp-res-ext="' + esc(fExt) + '" data-cp-res-name="' + esc(fName) + '">'
                + '<span class="cp-res-file-icon">' + getFileIcon(fExt, fName) + '</span>'
                + '<span class="cp-res-file-name">' + esc(fName) + '</span>'
                + '<span class="cp-res-file-size">' + formatSize(fSize) + '</span>'
                + '</div>';
        }
        return html;
    }

    function renderResourceFolders(folders) {
        var html = '';
        for (var i = 0; i < folders.length; i++) {
            var rf = folders[i];
            var rfName = g(rf, 'Name') || '';
            var rfPath = g(rf, 'RelativePath') || '';
            var rfFiles = g(rf, 'Files') || [];
            var rfSubs = g(rf, 'SubFolders') || [];
            var rfCount = countResourceTree(rfFiles, rfSubs);

            html += '<div class="cp-resource-folder">'
                + '<div class="cp-res-folder-hdr">'
                + '<span class="cp-res-icon"><i class="fa-solid fa-folder-open"></i></span>'
                + '<span class="cp-section-name">' + esc(rfName) + '</span>'
                + '<span class="cp-section-count">' + rfCount + ' files</span>'
                + '<button class="cp-res-dl-all" data-cp-zip-path="' + esc(rfPath) + '" title="Download all as zip">\u2b07</button>'
                + '<span class="cp-section-arrow">\u25b6</span>'
                + '</div>';
            html += '<div class="cp-res-folder-files">';
            html += renderResourceFiles(rfFiles);
            html += renderResourceFolders(rfSubs);
            html += '</div></div>';
        }
        return html;
    }

    function countResourceTree(files, folders) {
        var count = files.length;
        for (var i = 0; i < folders.length; i++) {
            var sub = folders[i];
            count += countResourceTree(g(sub, 'Files') || [], g(sub, 'SubFolders') || []);
        }
        return count;
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

    // Get the flat index of the first unplayed lesson across all sections.
    function getContinueIndex(sections) {
        var idx = 0;
        for (var i = 0; i < sections.length; i++) {
            var lessons = g(sections[i], 'Lessons') || [];
            for (var j = 0; j < lessons.length; j++) {
                if (!g(lessons[j], 'Played')) return idx;
                idx++;
            }
        }
        return 0; // all played — start from beginning
    }

    // Callback set by init() — clears lastUrl so the overview refreshes
    // when the user returns to the course page after playback.
    var invalidateOverview = function () {};

    // Play a list of item IDs using the Sessions API (max 100 to avoid URI limits).
    // startIndex tells Jellyfin which item in the list to begin playback from.
    function playItems(itemIds, startIndex) {
        if (!itemIds || !itemIds.length) return;
        var auth = getAuth();
        if (!auth) return;
        invalidateOverview();
        itemIds = itemIds.slice(0, 100);
        var startParam = (startIndex && startIndex > 0) ? '&startIndex=' + startIndex : '';
        apiFetch('/Sessions?ControllableByUserId=' + auth.userId)
            .then(function (sessions) {
                if (!sessions || !sessions.length) return;
                var sessionId = sessions[0].Id;
                return fetch(window.location.origin + '/Sessions/' + sessionId
                    + '/Playing?playCommand=PlayNow&itemIds=' + itemIds.join(',') + startParam, {
                    method: 'POST',
                    headers: { 'Authorization': 'MediaBrowser Token="' + auth.token + '"' }
                });
            })
            .catch(function () { });
    }

    // Load icon libraries (Font Awesome + Devicon) eagerly — they're tiny CSS-only
    function loadIconLibraries() {
        loadCdn('https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.1/css/all.min.css', 'css');
        loadCdn('https://cdn.jsdelivr.net/gh/devicons/devicon@latest/devicon.min.css', 'css');
    }

    // CDN library lazy-loading
    var _cdnLoaded = {};
    function loadCdn(url, type) {
        if (_cdnLoaded[url]) return _cdnLoaded[url];
        _cdnLoaded[url] = new Promise(function (resolve, reject) {
            var el;
            if (type === 'css') {
                el = document.createElement('link');
                el.rel = 'stylesheet';
                el.href = url;
            } else {
                el = document.createElement('script');
                el.src = url;
            }
            el.onload = resolve;
            el.onerror = reject;
            document.head.appendChild(el);
        });
        return _cdnLoaded[url];
    }

    var _previewModal = null;

    function createPreviewModal() {
        if (_previewModal) return _previewModal;
        _previewModal = document.createElement('div');
        _previewModal.className = 'cp-preview-overlay';
        _previewModal.innerHTML = '<div class="cp-preview-dialog">'
            + '<div class="cp-preview-header">'
            + '<span class="cp-preview-title"></span>'
            + '<div class="cp-preview-actions">'
            + '<button class="cp-preview-dl"><i class="fa-solid fa-download"></i> Download</button>'
            + '<button class="cp-preview-close"><i class="fa-solid fa-xmark"></i></button>'
            + '</div></div>'
            + '<div class="cp-preview-body"></div>'
            + '</div>';
        document.body.appendChild(_previewModal);

        _previewModal.querySelector('.cp-preview-close').addEventListener('click', closePreview);
        _previewModal.addEventListener('click', function (e) {
            if (e.target === _previewModal) closePreview();
        });
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && _previewModal.style.display === 'flex') closePreview();
        });
        return _previewModal;
    }

    function closePreview() {
        if (!_previewModal) return;
        _previewModal.style.display = 'none';
        _previewModal.querySelector('.cp-preview-body').innerHTML = '';
    }

    var _langMap = {
        py: 'python', js: 'javascript', mjs: 'javascript', cjs: 'javascript',
        jsx: 'javascript', ts: 'typescript', mts: 'typescript', cts: 'typescript',
        tsx: 'typescript', cs: 'csharp', sh: 'bash', bash: 'bash', zsh: 'bash',
        ps1: 'powershell', psm1: 'powershell', bat: 'dos', cmd: 'dos',
        yml: 'yaml', rs: 'rust', cpp: 'cpp', hpp: 'cpp', h: 'c',
        rb: 'ruby', kt: 'kotlin', kts: 'kotlin', pl: 'perl', pm: 'perl',
        tf: 'hcl', tfvars: 'hcl', hcl: 'hcl', gradle: 'groovy',
        scss: 'scss', sass: 'scss', less: 'less', vue: 'xml', svelte: 'xml',
        gql: 'graphql', graphql: 'graphql', proto: 'protobuf',
        md: 'markdown', markdown: 'markdown',
        cfg: 'ini', conf: 'ini', properties: 'properties',
        csv: 'plaintext', tsv: 'plaintext', log: 'plaintext', env: 'bash'
    };

    var _codeExtensions = [
        '.py','.js','.mjs','.cjs','.jsx','.ts','.mts','.cts','.tsx',
        '.java','.cs','.go','.rs','.rb','.php','.swift','.kt','.kts','.scala',
        '.lua','.pl','.pm','.r','.dart','.c','.cpp','.h','.hpp',
        '.sh','.bash','.zsh','.ps1','.psm1','.bat','.cmd',
        '.yml','.yaml','.json','.xml','.toml','.graphql','.gql','.proto',
        '.html','.css','.scss','.sass','.less','.vue','.svelte',
        '.sql','.tf','.tfvars','.hcl','.gradle',
        '.md','.markdown','.txt','.log','.cfg','.conf','.ini','.env',
        '.properties','.csv','.tsv'
    ];

    var _codeFileNames = [
        'dockerfile','jenkinsfile','vagrantfile','makefile','gnumakefile',
        'gemfile','rakefile','kustomization','procfile'
    ];

    function isCodePreviewable(ext, name) {
        if (ext && _codeExtensions.indexOf(ext) >= 0) return true;
        if (!ext && name) {
            var n = name.toLowerCase();
            if (_codeFileNames.indexOf(n) >= 0) return true;
            if (n === 'dockerfile' || n.indexOf('dockerfile.') === 0) return true;
        }
        return false;
    }

    function openResourcePreview(courseId, path, ext, name) {
        var modal = createPreviewModal();
        var dialog = modal.querySelector('.cp-preview-dialog');
        var body = dialog.querySelector('.cp-preview-body');
        var title = dialog.querySelector('.cp-preview-title');
        var dlBtn = dialog.querySelector('.cp-preview-dl');
        var fileName = name || path.split('/').pop();
        var fileUrl = resourceUrl(courseId, path);

        title.textContent = fileName;
        dlBtn.onclick = function () { window.open(fileUrl + '&download=true'); };
        body.innerHTML = '<div style="color:#888;padding:40px;text-align:center;">Loading...</div>';
        modal.style.display = 'flex';

        ext = (ext || '').toLowerCase();

        if (ext === '.pdf') {
            previewPdf(body, fileUrl);
        } else if (['.png','.jpg','.jpeg','.gif','.svg','.webp','.bmp','.ico'].indexOf(ext) >= 0) {
            previewImage(body, fileUrl);
        } else if (isCodePreviewable(ext, fileName)) {
            previewCode(body, fileUrl, ext, fileName);
        } else {
            body.innerHTML = '<div style="color:#888;padding:60px;text-align:center;">'
                + '<div style="font-size:2.5em;margin-bottom:16px;"><i class="fa-solid fa-file"></i></div>'
                + 'No preview available for this file type.'
                + '<br><br><button class="cp-preview-dl-fallback" style="background:#00a4dc;color:#fff;border:none;padding:8px 20px;border-radius:4px;cursor:pointer;">Download File</button>'
                + '</div>';
            body.querySelector('.cp-preview-dl-fallback').addEventListener('click', function() {
                window.open(fileUrl + '&download=true');
            });
        }
    }

    function previewPdf(container, fileUrl) {
        // Use browser's native PDF viewer via iframe — most reliable cross-browser.
        container.innerHTML = '<iframe src="' + fileUrl + '" style="width:100%;height:100%;border:none;background:#fff;"></iframe>';
    }

    function previewImage(container, fileUrl) {
        Promise.all([
            loadCdn('https://cdnjs.cloudflare.com/ajax/libs/viewerjs/1.11.7/viewer.min.js', 'js'),
            loadCdn('https://cdnjs.cloudflare.com/ajax/libs/viewerjs/1.11.7/viewer.min.css', 'css')
        ]).then(function () {
            container.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;width:100%;height:100%;">'
                + '<img src="' + fileUrl + '" style="max-width:100%;max-height:100%;object-fit:contain;" />'
                + '</div>';
            var img = container.querySelector('img');
            if (img && window.Viewer) {
                new Viewer(img, { inline: false, navbar: false, title: false, toolbar: { zoomIn: 1, zoomOut: 1, rotateLeft: 1, rotateRight: 1, reset: 1 } });
            }
        }).catch(function () {
            container.innerHTML = '<div style="display:flex;align-items:center;justify-content:center;width:100%;height:100%;">'
                + '<img src="' + fileUrl + '" style="max-width:100%;max-height:100%;object-fit:contain;" />'
                + '</div>';
        });
    }

    function previewCode(container, fileUrl, ext, name) {
        Promise.all([
            loadCdn('https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/highlight.min.js', 'js'),
            loadCdn('https://cdnjs.cloudflare.com/ajax/libs/highlight.js/11.9.0/styles/github-dark.min.css', 'css')
        ]).then(function () {
            return fetch(fileUrl).then(function (r) { return r.text(); });
        }).then(function (text) {
            var lang = ext.replace('.', '');
            // Handle extensionless files by name
            if (!lang && name) {
                var n = (name || '').toLowerCase();
                if (n === 'dockerfile' || (n.indexOf('dockerfile.') === 0)) lang = 'dockerfile';
                else if (n === 'jenkinsfile') lang = 'groovy';
                else if (n === 'makefile' || n === 'gnumakefile') lang = 'makefile';
                else if (n === 'vagrantfile' || n === 'gemfile' || n === 'rakefile') lang = 'ruby';
                else if (n === 'kustomization') lang = 'yaml';
                else if (n === 'procfile') lang = 'bash';
                else lang = 'plaintext';
            }
            lang = _langMap[lang] || lang;
            container.innerHTML = '<pre style="margin:0;height:100%;overflow:auto;"><code class="language-' + lang + '"></code></pre>';
            var codeEl = container.querySelector('code');
            codeEl.textContent = text;
            if (window.hljs) {
                window.hljs.highlightElement(codeEl);
            }
        }).catch(function () {
            fetch(fileUrl).then(function (r) { return r.text(); }).then(function (text) {
                container.innerHTML = '<pre style="margin:0;height:100%;overflow:auto;color:#ccc;padding:16px;font-size:0.85em;">' + esc(text) + '</pre>';
            });
        });
    }

    function injectStyles() {
        if (document.getElementById('cp-sidebar-styles')) return;
        var style = document.createElement('style');
        style.id = 'cp-sidebar-styles';
        style.textContent = ''
            // Sidebar shell
            + '.cp-sidebar { position: fixed; left: 0; bottom: 0; z-index: 999; background: #1e1e24; border-right: 1px solid #2a2a30; display: flex; flex-direction: column; transition: width 250ms ease; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; overflow: hidden; box-sizing: border-box; }'
            + '.cp-sidebar * { box-sizing: border-box; }'
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
            + '.cp-course-name { color: #eee; font-size: 1em; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }'
            + '.cp-course-meta { color: #666; font-size: 0.8em; }'
            + '.cp-close-btn { background: none; border: none; color: #555; cursor: pointer; font-size: 18px; padding: 4px 2px; line-height: 1; flex-shrink: 0; z-index: 1; }'
            + '.cp-close-btn:hover { color: #999; }'
            // Segmented progress
            + '.cp-seg-bar { display: flex; gap: 3px; height: 6px; margin: 10px 14px 0; }'
            + '.cp-seg { border-radius: 2px; transition: background 0.3s; }'
            + '.cp-seg.done { background: #4caf50; }'
            + '.cp-seg.active { background: #00a4dc; }'
            + '.cp-seg.pending { background: rgba(255,255,255,0.08); }'
            // Continue button
            + '.cp-continue-btn { margin: 10px 14px; background: linear-gradient(135deg, #00a4dc, #0090c4); color: #fff; border: none; padding: 10px; border-radius: 6px; font-size: 0.85em; font-weight: 600; cursor: pointer; text-align: center; transition: opacity 0.2s; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }'
            + '.cp-continue-btn:hover { opacity: 0.9; }'
            // Scrollable section list
            + '.cp-sections { flex: 1; overflow-y: auto; overflow-x: hidden; padding: 0 10px 10px; }'
            + '.cp-section { border-radius: 5px; margin-bottom: 4px; }'
            + '.cp-section.cp-sec-complete { opacity: 0.6; }'
            + '.cp-section.cp-sec-active { background: rgba(0,164,220,0.06); border: 1px solid rgba(0,164,220,0.15); }'
            + '.cp-section-hdr { padding: 8px; cursor: pointer; display: flex; align-items: center; gap: 6px; border-radius: 5px; }'
            + '.cp-section-hdr:hover { background: rgba(255,255,255,0.04); }'
            + '.cp-section-dot { font-size: 10px; flex-shrink: 0; }'
            + '.cp-section-name { flex: 1; font-size: 0.95em; color: #aaa; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }'
            + '.cp-section-name.cp-struck { text-decoration: line-through; }'
            + '.cp-section-count { font-size: 0.8em; color: #555; flex-shrink: 0; }'
            + '.cp-section-badge { font-size: 0.65em; color: #4caf50; flex-shrink: 0; }'
            + '.cp-section-play { background: none; border: 1px solid #444; color: #999; width: 22px; height: 22px; border-radius: 50%; cursor: pointer; display: flex; align-items: center; justify-content: center; font-size: 0.55em; transition: all 0.2s; flex-shrink: 0; padding: 0; }'
            + '.cp-section-play:hover { border-color: #00a4dc; color: #00a4dc; background: rgba(0,164,220,0.1); }'
            + '.cp-section-arrow { color: #666; transition: transform 200ms; font-size: 0.7em; flex-shrink: 0; }'
            + '.cp-section-hdr.open .cp-section-arrow { transform: rotate(90deg); }'
            // Lessons
            + '.cp-lessons { max-height: 0; overflow: hidden; transition: max-height 200ms ease-out; padding-left: 20px; border-left: 1px solid rgba(0,164,220,0.2); margin-left: 12px; }'
            + '.cp-lesson { display: flex; align-items: center; padding: 4px 0; gap: 6px; border-radius: 3px; }'
            + '.cp-lesson:hover { background: rgba(255,255,255,0.04); }'
            + '.cp-lesson.cp-playing { background: rgba(0,164,220,0.1); padding: 4px 6px; margin: 0 -6px; }'
            + '.cp-lesson.cp-lesson-next { background: rgba(0,164,220,0.08); border-left: 2px solid #00a4dc; padding-left: 4px; margin-left: -2px; }'
            + '.cp-lesson-status { width: 14px; text-align: center; cursor: pointer; flex-shrink: 0; font-size: 0.75em; }'
            + '.cp-lesson-status.played { color: #4caf50; }'
            + '.cp-lesson-status.unplayed { color: #444; }'
            + '.cp-lesson-name { flex: 1; min-width: 0; }'
            + '.cp-lesson-name a { color: #888; text-decoration: none; font-size: 0.9em; cursor: pointer; }'
            + '.cp-lesson-name a:hover { color: #00a4dc; }'
            + '.cp-next-badge { background: #00a4dc; color: #fff; font-size: 0.55em; padding: 1px 4px; border-radius: 2px; flex-shrink: 0; }'
            + '.cp-lesson-dur { color: #555; font-size: 0.8em; flex-shrink: 0; }'
            // Resize handle
            + '.cp-resize-handle { position: absolute; top: 0; right: -3px; width: 6px; height: 100%; cursor: col-resize; z-index: 1000; }'
            + '.cp-resize-handle::after { content: ""; position: absolute; top: 50%; left: 50%; transform: translate(-50%,-50%); width: 4px; height: 40px; background: rgba(255,255,255,0.1); border-radius: 2px; opacity: 0; transition: opacity 150ms; }'
            + '.cp-resize-handle:hover::after { opacity: 1; }'
            // Content push
            + '.cp-content-push { transition: margin-left 250ms ease; }'
            // File icons
            + '.cp-icon { font-size: 1em; width: 1.2em; text-align: center; }'
            // Resources
            + '.cp-res-icon { font-size: 0.85em; flex-shrink: 0; }'
            + '.cp-sec-resources { }'
            + '.cp-sec-res-hdr { padding: 6px 8px; cursor: pointer; display: flex; align-items: center; gap: 6px; border-radius: 4px; }'
            + '.cp-sec-res-hdr:hover { background: rgba(255,255,255,0.04); }'
            + '.cp-sec-res-label { flex: 1; font-size: 0.9em; color: #888; }'
            + '.cp-sec-res-hdr .cp-section-arrow { color: #666; transition: transform 200ms; font-size: 0.7em; }'
            + '.cp-sec-res-hdr.open .cp-section-arrow { transform: rotate(90deg); }'
            + '.cp-res-files { max-height: 0; overflow: hidden; transition: max-height 200ms ease-out; padding-left: 12px; }'
            + '.cp-resource-root { margin-top: 8px; }'
            + '.cp-res-root-body { max-height: 0; overflow: hidden; transition: max-height 200ms ease-out; padding-left: 16px; }'
            + '.cp-resource-folder { border-radius: 5px; margin-bottom: 4px; margin-top: 4px; }'
            + '.cp-res-folder-hdr { padding: 8px; cursor: pointer; display: flex; align-items: center; gap: 6px; border-radius: 5px; background: rgba(255,255,255,0.04); }'
            + '.cp-res-folder-hdr:hover { background: rgba(255,255,255,0.07); }'
            + '.cp-res-folder-hdr .cp-section-arrow { color: #666; transition: transform 200ms; font-size: 0.7em; }'
            + '.cp-res-folder-hdr.open .cp-section-arrow { transform: rotate(90deg); }'
            + '.cp-res-folder-files { max-height: 0; overflow: hidden; transition: max-height 200ms ease-out; padding-left: 20px; }'
            + '.cp-res-dl-all { background: none; border: 1px solid #444; color: #999; width: 22px; height: 22px; border-radius: 50%; cursor: pointer; display: flex; align-items: center; justify-content: center; font-size: 0.7em; transition: all 0.2s; flex-shrink: 0; padding: 0; }'
            + '.cp-res-dl-all:hover { border-color: #00a4dc; color: #00a4dc; background: rgba(0,164,220,0.1); }'
            + '.cp-res-file { display: flex; align-items: center; padding: 4px 6px; gap: 6px; border-radius: 3px; cursor: pointer; }'
            + '.cp-res-file:hover { background: rgba(255,255,255,0.04); }'
            + '.cp-res-file-icon { font-size: 0.8em; flex-shrink: 0; }'
            + '.cp-res-file-name { flex: 1; font-size: 0.9em; color: #888; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }'
            + '.cp-res-file-size { font-size: 0.8em; color: #555; flex-shrink: 0; }'
            // Preview modal
            + '.cp-preview-overlay { display: none; position: fixed; top: 0; left: 0; right: 0; bottom: 0; z-index: 10000; background: rgba(0,0,0,0.7); align-items: center; justify-content: center; }'
            + '.cp-preview-dialog { width: 80%; max-width: 1100px; height: 85vh; max-height: 85vh; background: #1a1a1e; border-radius: 12px; display: flex; flex-direction: column; overflow: hidden; box-shadow: 0 8px 40px rgba(0,0,0,0.6); }'
            + '.cp-preview-header { display: flex; align-items: center; justify-content: space-between; padding: 14px 20px; background: #222228; border-bottom: 1px solid #333; flex-shrink: 0; gap: 12px; }'
            + '.cp-preview-title { color: #eee; font-size: 1em; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; flex: 1; }'
            + '.cp-preview-actions { display: flex; gap: 10px; align-items: center; flex-shrink: 0; }'
            + '.cp-preview-dl { background: #00a4dc; color: #fff; border: none; padding: 8px 20px; border-radius: 6px; font-size: 0.9em; font-weight: 600; cursor: pointer; display: flex; align-items: center; gap: 6px; transition: background 0.2s; }'
            + '.cp-preview-dl:hover { background: #0090c4; }'
            + '.cp-preview-close { background: rgba(255,255,255,0.1); border: none; color: #aaa; font-size: 1.1em; cursor: pointer; width: 36px; height: 36px; border-radius: 8px; display: flex; align-items: center; justify-content: center; transition: all 0.2s; }'
            + '.cp-preview-close:hover { background: rgba(255,255,255,0.15); color: #fff; }'
            + '.cp-preview-body { flex: 1; overflow: auto; display: flex; flex-direction: column; }'
            + '@media (max-width: 767px) { .cp-preview-dialog { width: 95%; height: 90vh; border-radius: 8px; } }'
            // Responsive
            + '@media (max-width: 767px) { .cp-sidebar.cp-expanded { position: fixed; width: 85vw !important; max-width: 360px; box-shadow: 4px 0 20px rgba(0,0,0,0.5); } }'
            + '.cp-backdrop { position: fixed; top: 0; right: 0; bottom: 0; left: 0; background: rgba(0,0,0,0.5); z-index: 998; display: none; }';
        document.head.appendChild(style);
    }

    function getItemIdFromUrl() {
        var hash = window.location.hash || '';
        // Match id, parentId, or topParentId — Jellyfin uses different URL patterns
        // for detail pages vs folder/list views
        var idMatch = hash.match(/[?&]id=([a-f0-9-]+)/i);
        if (idMatch) return idMatch[1].replace(/-/g, '');
        var parentMatch = hash.match(/[?&]parentId=([a-f0-9-]+)/i);
        if (parentMatch) return parentMatch[1].replace(/-/g, '');
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

    // Find the folder ID to use for /Courses/{id}/Structure.
    // Any folder under a course library path is treated as a "course".
    // For non-folder items (lessons), use the parent folder.
    function findCourseFolderForItem(item) {
        var itemId = item.Id || item.id;

        // If this item is a folder under a course path, use it directly.
        if ((item.IsFolder || item.isFolder) && isCourseItem(item)) {
            return Promise.resolve(itemId);
        }

        // For non-folder items, check if the parent is a course folder.
        var parentId = item.ParentId || item.parentId;
        if (!parentId) return Promise.resolve(null);

        return apiFetch('/Items/' + parentId + '?Fields=Path,ParentId').then(function(parent) {
            if (!parent) return null;
            if ((parent.IsFolder || parent.isFolder) && isCourseItem(parent)) {
                return parent.Id || parent.id;
            }
            return null;
        });
    }

    function init() {
        injectStyles();
        loadIconLibraries();
        createSidebar();
        var lastUrl = '';
        var navGeneration = 0;
        invalidateOverview = function() { lastUrl = ''; };

        function onPageChange() {
            var url = window.location.hash;
            if (url === lastUrl) return;
            lastUrl = url;
            var gen = ++navGeneration;
            var itemId = getItemIdFromUrl();
            if (!itemId || !COURSE_PATHS || !COURSE_PATHS.length) {
                hideSidebar();
                return;
            }
            apiFetch('/Items/' + itemId + '?Fields=Path,ParentId').then(function(item) {
                if (gen !== navGeneration) return;
                if (!item) { hideSidebar(); return; }
                findCourseFolderForItem(item).then(function(courseId) {
                    if (gen !== navGeneration) return;
                    if (!courseId) { hideSidebar(); return; }
                    var auth = getAuth();
                    if (!auth) { hideSidebar(); return; }
                    apiFetch('/Courses/' + courseId + '/Structure?userId=' + auth.userId + '&_t=' + Date.now())
                        .then(function(data) {
                            if (gen !== navGeneration) return;
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
                        .catch(function() { if (gen === navGeneration) hideSidebar(); });
                }).catch(function() { if (gen === navGeneration) hideSidebar(); });
            }).catch(function() { if (gen === navGeneration) hideSidebar(); });
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
