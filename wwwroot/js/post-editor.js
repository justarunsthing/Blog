(function ($) {
    var languages = ['plaintext', 'csharp', 'sql', 'javascript', 'json', 'xml', 'bash', 'python', 'css', 'html'];
    var defaultLanguage = 'csharp';

    function insertCodeBlock(context, language) {
        if (languages.indexOf(language) === -1) {
            language = defaultLanguage;
        }

        var selected = context.invoke('editor.getSelectedText');
        var text = selected && selected.length ? selected : 'code';
        var encoded = $('<div>').text(text).html();
        context.invoke('editor.pasteHTML', '<pre><code class="language-' + language + '">' + encoded + '</code></pre><p><br></p>');
    }

    function codeBlockButton(context) {
        var ui = $.summernote.ui;
        return ui.buttonGroup([
            ui.button({
                className: 'dropdown-toggle',
                contents: '<i class="fas fa-code"></i> <span class="note-code-language-label">' + defaultLanguage + '</span> <i class="note-icon-caret"></i>',
                tooltip: 'Code block',
                data: {
                    toggle: 'dropdown'
                }
            }),
            ui.dropdown({
                className: 'note-code-language-menu',
                items: languages,
                title: 'Code language',
                itemClick: function (event) {
                    event.preventDefault();
                    var $item = $(event.target).closest('.note-dropdown-item');
                    var language = $item.attr('data-value');
                    if (languages.indexOf(language) === -1) {
                        language = defaultLanguage;
                    }

                    $item.closest('.note-btn-group').find('.note-code-language-label').text(language);
                    insertCodeBlock(context, language);
                },
                callback: function ($dropdown) {
                    $dropdown.on('mousedown', function (event) {
                        event.preventDefault();
                    });
                }
            })
        ]).render();
    }

    var $content = $('#Content');
    if (!$content.length || !$.fn.summernote) {
        return;
    }

    var toolbar = $.extend(true, [], $.summernote.options.toolbar);
    toolbar.push(['codeblock', ['codeBlock']]);
    $content.summernote({
        placeholder: 'Blog content',
        tabsize: 2,
        height: 280,
        toolbar: toolbar,
        buttons: {
            codeBlock: codeBlockButton
        }
    });
}(jQuery));
