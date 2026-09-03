(function ($) {
  'use strict';

  $(function () {
    $('#ready').text('yes');

    // The synchronous request, which is the reason this fixture exists: `async: false` is an
    // XMLHttpRequest that blocks the page's own thread until the origin answers, and a page that pumped its
    // event loop to serve it would be a page whose scripts could observe a turn inside another turn.
    var answer = null;
    $.ajax({
      url: '/jquery/data.json',
      async: false,
      dataType: 'json',
      success: function (data) { answer = data; },
    });

    $('#synchronous').text(answer === null ? 'nothing' : answer.rows.join(','));

    // Delegated events: the handler is on the list, not on a row, so it has to survive rows that did not
    // exist when it was registered.
    $('#list').on('click', 'li', function () {
      $('#clicked').text($(this).data('name'));
    });

    $('#add').on('click', function () {
      $('#list').append($('<li>').attr('data-name', 'three').text('three'));
    });
  });
})(jQuery);
