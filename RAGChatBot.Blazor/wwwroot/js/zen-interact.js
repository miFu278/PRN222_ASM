window.zenInteract = {
    initPageReveal: function () {
        if (typeof gsap === 'undefined') return;

        // Reset elements before animating to ensure they run on SPA nav
        gsap.set(".gsap-fade-up", { y: 30, opacity: 0 });
        gsap.to(".gsap-fade-up", { 
            y: 0, 
            opacity: 1, 
            duration: 1.2, 
            stagger: 0.1,
            ease: "power3.out" 
        });

        // Stagger list items if present
        gsap.set(".zen-list-item", { y: 20, opacity: 0 });
        gsap.to(".zen-list-item", {
            y: 0,
            opacity: 1,
            duration: 1.0,
            stagger: 0.1,
            ease: "power2.out",
            delay: 0.2
        });
    },

    initMagneticButtons: function () {
        if (typeof gsap === 'undefined') return;

        const magnets = document.querySelectorAll('.magnetic-btn');
        magnets.forEach(magnet => {
            // Remove previous event listeners if re-initializing
            const newMagnet = magnet.cloneNode(true);
            magnet.parentNode.replaceChild(newMagnet, magnet);

            newMagnet.addEventListener('mousemove', function(e) {
                const position = newMagnet.getBoundingClientRect();
                const x = e.clientX - position.left - position.width / 2;
                const y = e.clientY - position.top - position.height / 2;

                gsap.to(newMagnet, {
                    x: x * 0.3,
                    y: y * 0.3,
                    duration: 0.6,
                    ease: "power3.out"
                });
            });

            newMagnet.addEventListener('mouseleave', function() {
                gsap.to(newMagnet, {
                    x: 0,
                    y: 0,
                    duration: 0.6,
                    ease: "elastic.out(1, 0.3)"
                });
            });
        });
    },

    showToast: function (message, type = 'success') {
        const container = document.getElementById('zen-toast-container') || this.createToastContainer();
        
        const toast = document.createElement('div');
        toast.className = `zen-toast zen-toast-${type}`;
        
        const icon = type === 'success' ? 'fa-check' : 'fa-exclamation';
        toast.innerHTML = `<i class="fa-solid ${icon} me-2"></i> <span>${message}</span>`;
        
        container.appendChild(toast);

        // Animate in
        gsap.fromTo(toast, 
            { y: 50, opacity: 0 }, 
            { y: 0, opacity: 1, duration: 0.6, ease: "power3.out" }
        );

        // Animate out after 3 seconds
        setTimeout(() => {
            gsap.to(toast, {
                y: -20,
                opacity: 0,
                duration: 0.6,
                ease: "power2.in",
                onComplete: () => {
                    toast.remove();
                }
            });
        }, 3000);
    },

    createToastContainer: function () {
        const container = document.createElement('div');
        container.id = 'zen-toast-container';
        document.body.appendChild(container);
        return container;
    }
};
