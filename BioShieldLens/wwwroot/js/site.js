// BioShield Lens - Modern UI Interactions
console.log('🛡️ BioShield Lens loaded');

// Dark Mode Toggle
function initDarkMode() {
    console.log('🌙 Initializing dark mode...');
    const darkModeToggle = document.getElementById('darkModeToggle');
    const darkModeIcon = document.getElementById('darkModeIcon');
    const htmlElement = document.documentElement;
    const bodyElement = document.body;
    
    console.log('Dark mode button found:', darkModeToggle !== null);
    console.log('Dark mode icon found:', darkModeIcon !== null);
    
    // Check for saved theme preference or default to light mode
    // If no preference is saved, default to light mode
    let savedTheme = localStorage.getItem('theme');
    if (!savedTheme) {
        savedTheme = 'light';
        localStorage.setItem('theme', 'light');
    }
    console.log('Saved theme:', savedTheme);
    applyTheme(savedTheme);
    updateDarkModeIcon(savedTheme);
    
    if (darkModeToggle) {
        // Remove any existing listeners
        const newButton = darkModeToggle.cloneNode(true);
        darkModeToggle.parentNode.replaceChild(newButton, darkModeToggle);
        
        newButton.addEventListener('click', function(e) {
            e.preventDefault();
            console.log('🌙 Dark mode button clicked!');
            const currentTheme = htmlElement.getAttribute('data-theme');
            const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
            console.log('Switching from', currentTheme, 'to', newTheme);
            
            applyTheme(newTheme);
            localStorage.setItem('theme', newTheme);
            updateDarkModeIcon(newTheme);
            
            // Add smooth transition
            if (bodyElement) {
                bodyElement.style.transition = 'background-color 0.3s ease, color 0.3s ease';
                setTimeout(() => {
                    bodyElement.style.transition = '';
                }, 300);
            }
        });
        console.log('✅ Dark mode event listener attached');
    } else {
        console.error('❌ Dark mode button not found!');
    }
    
    function applyTheme(theme) {
        console.log('Applying theme:', theme);
        htmlElement.setAttribute('data-theme', theme);
        if (bodyElement) {
            bodyElement.setAttribute('data-theme', theme);
        }
        console.log('Theme applied. HTML data-theme:', htmlElement.getAttribute('data-theme'));
    }
    
    function updateDarkModeIcon(theme) {
        const icon = document.getElementById('darkModeIcon');
        const button = document.getElementById('darkModeToggle');
        if (icon) {
            // Icon represents what you'll switch TO: moon when in light (switch to dark), sun when in dark (switch to light)
            if (theme === 'dark') {
                // Currently dark, show sun icon (click to switch to light)
                icon.classList.remove('bi-moon-fill');
                icon.classList.add('bi-sun-fill');
                if (button) button.setAttribute('title', 'Switch to Light Mode');
                console.log('Icon changed to sun (currently dark, click to go light)');
            } else {
                // Currently light, show moon icon (click to switch to dark)
                icon.classList.remove('bi-sun-fill');
                icon.classList.add('bi-moon-fill');
                if (button) button.setAttribute('title', 'Switch to Dark Mode');
                console.log('Icon changed to moon (currently light, click to go dark)');
            }
        }
    }
}

// Auto-Refresh Functionality
let autoRefreshInterval;
let countdownInterval;
let refreshEnabled = true;
let secondsRemaining = 300; // 5 minutes default

function initAutoRefresh() {
    const refreshNowBtn = document.getElementById('refreshNowBtn');
    const toggleAutoRefresh = document.getElementById('toggleAutoRefresh');
    const refreshCountdown = document.getElementById('refreshCountdown');
    const lastUpdated = document.getElementById('lastUpdated');
    const autoRefreshText = document.getElementById('autoRefreshText');
    
    if (!refreshNowBtn) return; // Exit if not on dashboard
    
    // Update last updated time
    updateLastUpdatedTime();
    
    // Start countdown
    startCountdown();
    
    // Refresh now button
    refreshNowBtn.addEventListener('click', function() {
        refreshPage();
    });
    
    // Toggle auto-refresh
    toggleAutoRefresh.addEventListener('click', function() {
        refreshEnabled = !refreshEnabled;
        
        if (refreshEnabled) {
            autoRefreshText.textContent = 'Pause';
            toggleAutoRefresh.querySelector('i').classList.remove('bi-play-fill');
            toggleAutoRefresh.querySelector('i').classList.add('bi-pause-fill');
            startCountdown();
        } else {
            autoRefreshText.textContent = 'Resume';
            toggleAutoRefresh.querySelector('i').classList.remove('bi-pause-fill');
            toggleAutoRefresh.querySelector('i').classList.add('bi-play-fill');
            stopCountdown();
        }
    });
    
    function updateLastUpdatedTime() {
        if (lastUpdated) {
            const now = new Date();
            const timeString = now.toLocaleTimeString('en-US', { 
                hour: '2-digit', 
                minute: '2-digit', 
                second: '2-digit' 
            });
            lastUpdated.innerHTML = `Last updated: <strong>${timeString}</strong>`;
        }
    }
    
    function startCountdown() {
        if (!refreshEnabled) return;
        
        secondsRemaining = 300; // Reset to 5 minutes
        updateCountdownDisplay();
        
        countdownInterval = setInterval(() => {
            secondsRemaining--;
            updateCountdownDisplay();
            
            if (secondsRemaining <= 0) {
                refreshPage();
            }
        }, 1000);
    }
    
    function stopCountdown() {
        if (countdownInterval) {
            clearInterval(countdownInterval);
        }
    }
    
    function updateCountdownDisplay() {
        if (refreshCountdown) {
            const minutes = Math.floor(secondsRemaining / 60);
            const seconds = secondsRemaining % 60;
            const timeString = `${minutes}:${seconds.toString().padStart(2, '0')}`;
            refreshCountdown.innerHTML = `Auto-refresh in: <strong>${timeString}</strong>`;
        }
    }
    
    function refreshPage() {
        // Show loading state
        if (refreshNowBtn) {
            refreshNowBtn.innerHTML = '<i class="bi bi-arrow-clockwise spin"></i> Refreshing...';
            refreshNowBtn.disabled = true;
        }
        
        // Reload page
        window.location.reload();
    }
}

// Smooth scroll behavior
document.addEventListener('DOMContentLoaded', function() {
    console.log('📄 DOM Content Loaded');
    // Re-initialize dark mode to ensure event listeners are attached
    initDarkMode();
    
    // Initialize auto-refresh
    initAutoRefresh();
    // Add smooth fade-in animation on page load
    const cards = document.querySelectorAll('.card');
    cards.forEach((card, index) => {
        card.style.opacity = '0';
        card.style.transform = 'translateY(20px)';
        setTimeout(() => {
            card.style.transition = 'all 0.5s cubic-bezier(0.4, 0, 0.2, 1)';
            card.style.opacity = '1';
            card.style.transform = 'translateY(0)';
        }, index * 100);
    });

    // Enhanced navbar behavior on scroll
    let lastScrollTop = 0;
    const navbar = document.querySelector('.navbar');
    
    window.addEventListener('scroll', function() {
        let scrollTop = window.pageYOffset || document.documentElement.scrollTop;
        
        if (scrollTop > 50) {
            navbar.style.boxShadow = '0 4px 16px rgba(0, 0, 0, 0.1)';
        } else {
            navbar.style.boxShadow = '0 2px 8px rgba(0, 0, 0, 0.04)';
        }
        
        lastScrollTop = scrollTop;
    });

    // Add ripple effect to buttons
    const buttons = document.querySelectorAll('.btn');
    buttons.forEach(button => {
        button.addEventListener('click', function(e) {
            const ripple = document.createElement('span');
            const rect = button.getBoundingClientRect();
            const size = Math.max(rect.width, rect.height);
            const x = e.clientX - rect.left - size / 2;
            const y = e.clientY - rect.top - size / 2;
            
            ripple.style.width = ripple.style.height = size + 'px';
            ripple.style.left = x + 'px';
            ripple.style.top = y + 'px';
            ripple.classList.add('ripple');
            
            button.appendChild(ripple);
            
            setTimeout(() => {
                ripple.remove();
            }, 600);
        });
    });

    // Table row hover effect enhancement
    const tableRows = document.querySelectorAll('.table tbody tr');
    tableRows.forEach(row => {
        row.addEventListener('mouseenter', function() {
            this.style.transition = 'all 0.2s ease';
        });
    });

    // Enhanced card hover effects
    const hoverCards = document.querySelectorAll('.card');
    hoverCards.forEach(card => {
        card.addEventListener('mouseenter', function() {
            this.style.transition = 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)';
        });
    });

    // Auto-dismiss alerts
    const alerts = document.querySelectorAll('.alert');
    alerts.forEach(alert => {
        setTimeout(() => {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 5000);
    });

    // Number counter animation for stat cards
    const statNumbers = document.querySelectorAll('.stat-number');
    statNumbers.forEach(stat => {
        const finalValue = parseInt(stat.textContent);
        if (!isNaN(finalValue)) {
            let currentValue = 0;
            const increment = Math.max(1, Math.ceil(finalValue / 30));
            const timer = setInterval(() => {
                currentValue += increment;
                if (currentValue >= finalValue) {
                    currentValue = finalValue;
                    clearInterval(timer);
                }
                stat.textContent = currentValue;
            }, 30);
        }
    });
});

// Add ripple CSS dynamically
const style = document.createElement('style');
style.textContent = `
    .btn {
        position: relative;
        overflow: hidden;
    }
    
    .ripple {
        position: absolute;
        border-radius: 50%;
        background: rgba(255, 255, 255, 0.5);
        transform: scale(0);
        animation: ripple-animation 0.6s ease-out;
        pointer-events: none;
    }
    
    @keyframes ripple-animation {
        to {
            transform: scale(2);
            opacity: 0;
        }
    }
`;
document.head.appendChild(style);

