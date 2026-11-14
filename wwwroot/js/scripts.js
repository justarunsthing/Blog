/*!
* Start Bootstrap - Clean Blog v6.0.9 (https://startbootstrap.com/theme/clean-blog)
* Copyright 2013-2023 Start Bootstrap
* Licensed under MIT (https://github.com/StartBootstrap/startbootstrap-clean-blog/blob/master/LICENSE)
*/
document.addEventListener('submit', (event) => {
    const form = event.target;

    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    if (form.dataset.submitting === 'true') {
        event.preventDefault();
        event.stopPropagation();
        return;
    }

    if (window.jQuery) {
        const $form = window.jQuery(form);
        $form.find('textarea').each(function () {
            const $editor = window.jQuery(this);
            if ($editor.data('summernote')) {
                $editor.val($editor.summernote('code'));
            }
        });

        if ($form.data('validator') && !$form.valid()) {
            return;
        }
    }

    form.dataset.submitting = 'true';
    window.setTimeout(() => {
        if (event.defaultPrevented) {
            delete form.dataset.submitting;
            return;
        }

        form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach((control) => {
            control.disabled = true;
        });
    }, 0);
}, true);

window.addEventListener('pageshow', () => {
    document.querySelectorAll('form[data-submitting]').forEach((form) => {
        delete form.dataset.submitting;
        form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach((control) => {
            control.disabled = false;
        });
    });
});

window.addEventListener('DOMContentLoaded', () => {
    let scrollPos = 0;
    const mainNav = document.getElementById('mainNav');
    const headerHeight = mainNav.clientHeight;

    window.addEventListener('scroll', function () {
        const currentTop = document.body.getBoundingClientRect().top * -1;

        if (currentTop < scrollPos) {
            // Scrolling Up
            if (currentTop > 0 && mainNav.classList.contains('is-fixed')) {
                mainNav.classList.add('is-visible');
            } else {
                mainNav.classList.remove('is-visible', 'is-fixed');
            }
        } else {
            // Scrolling Down
            mainNav.classList.remove(['is-visible']);
            if (currentTop > headerHeight && !mainNav.classList.contains('is-fixed')) {
                mainNav.classList.add('is-fixed');
            }
        }

        scrollPos = currentTop;
    });
});
