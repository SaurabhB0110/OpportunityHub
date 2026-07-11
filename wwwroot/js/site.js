document.addEventListener("DOMContentLoaded", () => {
    const saved = new Set(JSON.parse(localStorage.getItem("opportunityhub-saved") || "[]"));
    document.querySelectorAll("[data-save-id]").forEach(button => {
        const id = button.dataset.saveId;
        if (saved.has(id)) button.classList.add("saved");
        button.addEventListener("click", () => {
            if (saved.has(id)) saved.delete(id); else saved.add(id);
            localStorage.setItem("opportunityhub-saved", JSON.stringify([...saved]));
            document.querySelectorAll(`[data-save-id="${id}"]`).forEach(item => item.classList.toggle("saved", saved.has(id)));
        });
    });
});

// Form submission loading state
document.addEventListener('DOMContentLoaded', function() {
    // Prevent double-click on form submissions
    const forms = document.querySelectorAll('form');
    forms.forEach(form => {
        form.addEventListener('submit', function(e) {
            const submitBtn = this.querySelector('button[type="submit"]');
            if (submitBtn) {
                if (submitBtn.classList.contains('loading')) {
                    e.preventDefault();
                    return false;
                }
                submitBtn.classList.add('loading');
                submitBtn.disabled = true;
            }
        });
    });

    // Dismiss alerts on close button click
    const dismissButtons = document.querySelectorAll('.alert-close');
    dismissButtons.forEach(btn => {
        btn.addEventListener('click', function() {
            this.closest('.alert').remove();
        });
    });

    // Auto-dismiss success/info alerts after 5 seconds
    const autoDissmissAlerts = document.querySelectorAll('.alert-success, .alert-info');
    autoDissmissAlerts.forEach(alert => {
        setTimeout(() => {
            if (alert.parentElement) {
                alert.remove();
            }
        }, 5000);
    });

    // Add keyboard navigation to modal forms
    document.addEventListener('keydown', function(e) {
        if (e.key === 'Escape') {
            const modal = document.querySelector('.modal.show');
            if (modal) {
                const bootstrapModal = bootstrap.Modal.getInstance(modal);
                if (bootstrapModal) {
                    bootstrapModal.hide();
                }
            }
        }
    });
});

// Toast notification system
function showToast(message, type = 'info') {
    const container = document.querySelector('.toast-container') || createToastContainer();
    
    const toast = document.createElement('div');
    toast.className = `toast ${type}`;
    toast.innerHTML = `
        <span class="toast-icon">${getToastIcon(type)}</span>
        <div class="toast-message">${message}</div>
        <button class="toast-close" onclick="this.parentElement.remove()">×</button>
    `;
    
    container.appendChild(toast);
    
    setTimeout(() => {
        if (toast.parentElement) {
            toast.remove();
        }
    }, 4000);
}

function createToastContainer() {
    const container = document.createElement('div');
    container.className = 'toast-container';
    document.body.appendChild(container);
    return container;
}

function getToastIcon(type) {
    const icons = {
        'success': '<i class="bi bi-check2-circle"></i>',
        'error': '<i class="bi bi-exclamation-octagon"></i>',
        'info': '<i class="bi bi-info-circle"></i>',
        'warning': '<i class="bi bi-exclamation-triangle"></i>'
    };
    return icons[type] || icons['info'];
}

/* Admin enhancements (lightweight, non-invasive) */
(function () {
    // Ensure sticky table headers render properly by adjusting top offset on small screens
    function adjustStickyOffset() {
        document.querySelectorAll('.admin-table thead th').forEach(th => {
            th.style.top = window.innerWidth < 768 ? '56px' : '0';
        });
    }
    window.addEventListener('resize', adjustStickyOffset);
    document.addEventListener('DOMContentLoaded', adjustStickyOffset);

    // Add subtle hover elevation for rows already provided via CSS; no further JS changes.
})();
