(function () {
  'use strict';

  // gh ledger export's report script. The page is complete without it; it adds zooming and panning to the timeline, the
  // cost lens switch, and sortable tables. It reads only the ledger-data block and the page's own elements, writes text
  // only through textContent, and never contacts the network.

  var data = null;
  try {
    var block = document.getElementById('ledger-data');
    data = block ? JSON.parse(block.textContent) : null;
  } catch (error) {
    data = null;
  }

  if (data && data.timeline) {
    setUpTimeline(data.timeline);
  }

  setUpLenses();
  setUpSorting();

  function setUpTimeline(timeline) {
    var plot = document.getElementById('timeline-plot');
    var axis = document.getElementById('timeline-axis');
    var labels = document.getElementById('timeline-axis-labels');
    var overview = document.getElementById('timeline-overview');
    var frame = document.getElementById('timeline-window');
    var viewport = document.getElementById('timeline-viewport');
    var controls = document.getElementById('timeline-controls');
    var reset = document.getElementById('timeline-reset');
    if (!plot || !axis || !labels || !overview || !frame || !viewport || !controls || !reset) {
      return;
    }

    var width = timeline.width;
    var height = Number(plot.getAttribute('height'));
    var ticks = Array.isArray(timeline.ticks) ? timeline.ticks : [];
    var view = { x: 0, w: width };
    var pointers = new Map();
    var drag = null;
    var pinch = null;
    var brush = null;

    controls.hidden = false;

    function clamp(value, low, high) {
      return Math.min(Math.max(value, low), high);
    }

    function apply() {
      view.w = clamp(view.w, 0.5, width);
      view.x = clamp(view.x, 0, width - view.w);
      plot.setAttribute('viewBox', view.x + ' 0 ' + view.w + ' ' + height);
      axis.setAttribute('viewBox', view.x + ' 0 ' + view.w + ' 6');
      frame.setAttribute('x', String(view.x));
      frame.setAttribute('width', String(view.w));
      renderLabels();
    }

    // The finest level whose ticks in view, counting coarser levels, number at most 12; at level 0, every n-th day.
    function shownTicks() {
      var inView = ticks.filter(function (tick) {
        return tick.x >= view.x && tick.x <= view.x + view.w;
      });
      for (var level = 3; level >= 0; level--) {
        var shown = inView.filter(function (tick) {
          return tick.level <= level;
        });
        if (shown.length <= 12) {
          return shown;
        }
      }

      var days = inView.filter(function (tick) {
        return tick.level === 0;
      });
      var every = Math.ceil(days.length / 12);
      return days.filter(function (tick, index) {
        return index % every === 0;
      });
    }

    function renderLabels() {
      labels.textContent = '';
      shownTicks().forEach(function (tick) {
        var label = document.createElement('span');
        label.className = 'axis-label';
        label.textContent = String(tick.label);
        label.style.left = ((tick.x - view.x) / view.w * 100).toFixed(4) + '%';
        labels.appendChild(label);
      });
    }

    // The timeline unit under a client x coordinate.
    function unitAt(clientX) {
      var box = plot.getBoundingClientRect();
      return view.x + (clientX - box.left) / Math.max(box.width, 1) * view.w;
    }

    function zoom(factor, unit) {
      var ratio = (unit - view.x) / view.w;
      view.w = clamp(view.w * factor, 0.5, width);
      view.x = unit - ratio * view.w;
      apply();
    }

    function resetView() {
      view.x = 0;
      view.w = width;
      apply();
    }

    viewport.addEventListener('wheel', function (event) {
      event.preventDefault();
      zoom(event.deltaY < 0 ? 1 / 1.25 : 1.25, unitAt(event.clientX));
    }, { passive: false });

    viewport.addEventListener('pointerdown', function (event) {
      viewport.setPointerCapture(event.pointerId);
      pointers.set(event.pointerId, event.clientX);
      if (pointers.size === 1) {
        drag = { clientX: event.clientX, x: view.x };
        pinch = null;
      } else if (pointers.size === 2) {
        var positions = Array.from(pointers.values());
        drag = null;
        pinch = {
          distance: Math.max(Math.abs(positions[0] - positions[1]), 1),
          w: view.w,
          unit: unitAt((positions[0] + positions[1]) / 2)
        };
      }
    });

    viewport.addEventListener('pointermove', function (event) {
      if (!pointers.has(event.pointerId)) {
        return;
      }

      pointers.set(event.pointerId, event.clientX);
      if (pinch && pointers.size === 2) {
        var positions = Array.from(pointers.values());
        var distance = Math.max(Math.abs(positions[0] - positions[1]), 1);
        var box = plot.getBoundingClientRect();
        var middle = (positions[0] + positions[1]) / 2;
        view.w = clamp(pinch.w * pinch.distance / distance, 0.5, width);
        view.x = pinch.unit - (middle - box.left) / Math.max(box.width, 1) * view.w;
        apply();
      } else if (drag) {
        var bounds = plot.getBoundingClientRect();
        view.x = drag.x - (event.clientX - drag.clientX) / Math.max(bounds.width, 1) * view.w;
        apply();
      }
    });

    function release(event) {
      pointers.delete(event.pointerId);
      if (pointers.size < 2) {
        pinch = null;
      }

      if (pointers.size === 0) {
        drag = null;
      }
    }

    viewport.addEventListener('pointerup', release);
    viewport.addEventListener('pointercancel', release);

    viewport.addEventListener('keydown', function (event) {
      var handled = true;
      if (event.key === 'ArrowLeft') {
        view.x -= view.w * 0.1;
        apply();
      } else if (event.key === 'ArrowRight') {
        view.x += view.w * 0.1;
        apply();
      } else if (event.key === '+' || event.key === '=') {
        zoom(1 / 1.25, view.x + view.w / 2);
      } else if (event.key === '-') {
        zoom(1.25, view.x + view.w / 2);
      } else if (event.key === '0' || event.key === 'Home') {
        resetView();
      } else {
        handled = false;
      }

      if (handled) {
        event.preventDefault();
      }
    });

    reset.addEventListener('click', resetView);

    // The overview's unit under a client x coordinate.
    function overviewUnit(clientX) {
      var box = overview.getBoundingClientRect();
      return clamp((clientX - box.left) / Math.max(box.width, 1) * width, 0, width);
    }

    overview.addEventListener('pointerdown', function (event) {
      // The overview is inside the viewport: choosing a range mustn't also start a pan.
      event.stopPropagation();
      overview.setPointerCapture(event.pointerId);
      brush = overviewUnit(event.clientX);
    });

    overview.addEventListener('pointermove', function (event) {
      if (brush === null) {
        return;
      }

      var unit = overviewUnit(event.clientX);
      frame.setAttribute('x', String(Math.min(brush, unit)));
      frame.setAttribute('width', String(Math.abs(unit - brush)));
    });

    overview.addEventListener('pointerup', function (event) {
      if (brush === null) {
        return;
      }

      var unit = overviewUnit(event.clientX);
      var start = Math.min(brush, unit);
      var span = Math.abs(unit - brush);
      brush = null;
      if (span < 1) {
        view.x = unit - view.w / 2;
      } else {
        view.x = start;
        view.w = span;
      }

      apply();
    });

    overview.addEventListener('pointercancel', function () {
      brush = null;
      apply();
    });

    apply();
  }

  function setUpLenses() {
    var toggle = document.getElementById('lens-toggle');
    if (!toggle) {
      return;
    }

    var figures = Array.from(document.querySelectorAll('figure[data-lens]'));
    function show(lens) {
      figures.forEach(function (figure) {
        figure.hidden = figure.getAttribute('data-lens') !== lens;
      });
    }

    toggle.hidden = false;
    toggle.addEventListener('change', function (event) {
      if (event.target && event.target.name === 'lens') {
        show(event.target.value);
      }
    });
    var checked = toggle.querySelector('input[name="lens"]:checked');
    show(checked ? checked.value : 'billed');
  }

  // Each sortable table's headers become buttons that sort its rows by their integer data-sort ranks.
  function setUpSorting() {
    Array.from(document.querySelectorAll('table.sortable')).forEach(function (table) {
      var body = table.tBodies[0];
      var headers = Array.from(table.querySelectorAll('thead th'));
      if (!body) {
        return;
      }

      headers.forEach(function (header, column) {
        var button = document.createElement('button');
        button.type = 'button';
        while (header.firstChild) {
          button.appendChild(header.firstChild);
        }

        header.appendChild(button);
        button.addEventListener('click', function () {
          var ascending = header.getAttribute('aria-sort') !== 'ascending';
          headers.forEach(function (other) {
            other.removeAttribute('aria-sort');
          });
          header.setAttribute('aria-sort', ascending ? 'ascending' : 'descending');
          var rows = Array.from(body.rows);
          rows.sort(function (left, right) {
            var difference = Number(left.cells[column].getAttribute('data-sort')) -
              Number(right.cells[column].getAttribute('data-sort'));
            return ascending ? difference : -difference;
          });
          rows.forEach(function (row) {
            body.appendChild(row);
          });
        });
      });
    });
  }
}());
