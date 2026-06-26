window.zenInteract = {
    initLenis: function () {
        if (typeof Lenis === 'undefined' || typeof gsap === 'undefined') return;

        // Tự động tắt Lenis nếu người dùng bật chế độ giảm hoạt ảnh (Accessibility)
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            document.documentElement.style.scrollBehavior = 'auto';
            return;
        }

        if (window.lenis) {
            window.lenis.destroy();
        }

        window.lenis = new Lenis({
            duration: 1.2,
            easing: (t) => Math.min(1, 1.001 - Math.pow(2, -10 * t)),
            orientation: 'vertical',
            gestureOrientation: 'vertical',
            smoothWheel: true,
            wheelMultiplier: 1,
            smoothTouch: false,
            touchMultiplier: 2,
            infinite: false,
        });

        window.lenis.on('scroll', ScrollTrigger.update);

        gsap.ticker.add((time) => {
            window.lenis.raf(time * 1000);
        });

        gsap.ticker.lagSmoothing(0);
    },

    initPageReveal: function () {
        if (typeof gsap === 'undefined') return;

        const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

        if (prefersReducedMotion) {
            gsap.set(".gsap-fade-up, .zen-list-item", { y: 0, opacity: 1 });
            return;
        }

        gsap.set(".gsap-fade-up", { y: 30, opacity: 0 });
        gsap.to(".gsap-fade-up", { 
            y: 0, 
            opacity: 1, 
            duration: 1.2, 
            stagger: 0.1,
            ease: "power3.out" 
        });

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

        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

        const magnets = document.querySelectorAll('.magnetic-btn');
        magnets.forEach(magnet => {
            if (magnet.dataset.magneticInit) return;
            magnet.dataset.magneticInit = 'true';

            magnet.addEventListener('mousemove', function(e) {
                const position = magnet.getBoundingClientRect();
                const x = e.clientX - position.left - position.width / 2;
                const y = e.clientY - position.top - position.height / 2;

                gsap.to(magnet, {
                    x: x * 0.3,
                    y: y * 0.3,
                    duration: 0.6,
                    ease: "power3.out"
                });
            });

            magnet.addEventListener('mouseleave', function() {
                gsap.to(magnet, {
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

        const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (prefersReducedMotion) {
            toast.style.opacity = 1;
            toast.style.transform = 'none';
        } else {
            gsap.fromTo(toast, 
                { y: 50, opacity: 0 }, 
                { y: 0, opacity: 1, duration: 0.6, ease: "power3.out" }
            );
        }

        setTimeout(() => {
            if (prefersReducedMotion) {
                toast.remove();
            } else {
                gsap.to(toast, {
                    y: -20,
                    opacity: 0,
                    duration: 0.6,
                    ease: "power2.in",
                    onComplete: () => {
                        toast.remove();
                    }
                });
            }
        }, 3000);
    },

    createToastContainer: function () {
        const container = document.createElement('div');
        container.id = 'zen-toast-container';
        document.body.appendChild(container);
        return container;
    }
};
