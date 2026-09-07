window.jQuery(() => {
        const cedula = document.getElementById('Cedula');
        const nacimiento = document.getElementById('FechaNacimiento');
        const marker = document.getElementById('cedula-required');
        const help = document.getElementById('cedula-help');
        const $ = window.jQuery;
        let rejected = false;
        let previous = cedula.value;
        const format = digits => digits.slice(0, 3) + (digits.length >= 3 ? '-' : '')
            + digits.slice(3, 10) + (digits.length >= 10 ? '-' : '') + digits.slice(10);
        const digitsOf = value => value.replace(/-/g, '');
        const complete = value => /^[0-9]{3}-[0-9]{7}-[0-9]$/.test(value);
        const message = 'Ingresa una cédula completa de 11 dígitos con formato XXX-XXXXXXX-X.';
        const validate = () => {
            const invalid = rejected || (cedula.value !== '' && !complete(cedula.value));
            cedula.setCustomValidity(invalid ? message : cedula.required && !cedula.value
                ? 'La cédula es obligatoria para pacientes de 18 años o más.' : '');
        };
        const refreshValidation = () => {
            validate();
            if ($?.validator) $(cedula).valid();
        };
        const adult = () => {
            if (!/^\d{4}-\d{2}-\d{2}$/.test(nacimiento.value)) return false;
            // Comparamos componentes sin interpretar la fecha como UTC; el día procede del servidor.
            const birth = nacimiento.value.split('-').map(Number);
            const today = cedula.dataset.today.split('-').map(Number);
            let age = today[0] - birth[0];
            if (birth[1] > today[1] || (birth[1] === today[1] && birth[2] > today[2])) age--;
            return age >= 18;
        };
        const updateRequired = () => {
            cedula.required = adult();
            cedula.setAttribute('aria-required', String(cedula.required));
            marker.hidden = !cedula.required;
            help.textContent = cedula.required ? 'Obligatoria para pacientes de 18 años o más.'
                : 'Opcional. Obligatoria a partir de los 18 años.';
            refreshValidation();
        };
        if ($?.validator) {
            $.validator.addMethod('cedulaCompleta', value => !rejected && (value === '' || complete(value)), message);
            $(cedula).rules('add', {
                required: { depends: adult },
                cedulaCompleta: true,
                messages: { required: 'La cédula es obligatoria para pacientes de 18 años o más.' }
            });
        }
        const reject = () => {
            rejected = true;
            refreshValidation();
        };
        const apply = (value, digitPosition) => {
            rejected = false;
            cedula.value = format(digitsOf(value));
            previous = cedula.value;
            let position = digitPosition + (digitPosition >= 3 ? 1 : 0) + (digitPosition >= 10 ? 1 : 0);
            position = Math.min(position, cedula.value.length);
            cedula.setSelectionRange(position, position);
            refreshValidation();
        };
        cedula.addEventListener('beforeinput', event => {
            if (!event.inputType.startsWith('insert') || event.data == null) return;
            const candidate = cedula.value.slice(0, cedula.selectionStart) + event.data
                + cedula.value.slice(cedula.selectionEnd);
            if (!/^[0-9-]*$/.test(event.data) || digitsOf(candidate).length > 11) {
                event.preventDefault();
                reject();
            }
        });
        cedula.addEventListener('paste', event => {
            event.preventDefault();
            const text = event.clipboardData.getData('text').trim();
            const start = cedula.selectionStart;
            const candidate = cedula.value.slice(0, start) + text + cedula.value.slice(cedula.selectionEnd);
            if (!/^(?:[0-9]+|[0-9]{3}-[0-9]{7}-[0-9])$/.test(text) || digitsOf(candidate).length > 11) {
                reject();
                return;
            }
            apply(candidate, digitsOf(cedula.value.slice(0, start) + text).length);
        });
        cedula.addEventListener('keydown', event => {
            const start = cedula.selectionStart;
            if (start !== cedula.selectionEnd) return;
            // Al borrar junto a un separador, eliminamos el dígito contiguo para evitar un cursor atrapado.
            if (event.key === 'Backspace' && cedula.value[start - 1] === '-') {
                event.preventDefault();
                const position = digitsOf(cedula.value.slice(0, start)).length;
                const digits = digitsOf(cedula.value);
                apply(digits.slice(0, position - 1) + digits.slice(position), position - 1);
            } else if (event.key === 'Delete' && cedula.value[start] === '-') {
                event.preventDefault();
                const position = digitsOf(cedula.value.slice(0, start)).length;
                const digits = digitsOf(cedula.value);
                apply(digits.slice(0, position) + digits.slice(position + 1), position);
            }
        });
        cedula.addEventListener('input', () => {
            const value = cedula.value;
            if (!/^[0-9-]*$/.test(value) || digitsOf(value).length > 11) {
                cedula.value = previous;
                reject();
                return;
            }
            apply(value, digitsOf(value.slice(0, cedula.selectionStart)).length);
        });
        cedula.addEventListener('blur', refreshValidation);
        nacimiento.addEventListener('input', updateRequired);
        nacimiento.addEventListener('change', updateRequired);
        if (/^(?:[0-9]{11}|[0-9]{3}-[0-9]{7}-[0-9])$/.test(cedula.value)) {
            cedula.value = format(digitsOf(cedula.value));
            previous = cedula.value;
        }
        updateRequired();

        const selector = document.getElementById('seguro-selector');
        const seguro = document.getElementById('SeguroId');
        const radios = document.querySelectorAll('input[name="TieneSeguro"]');
        const sexo = document.getElementById('Sexo');
        const embarazoField = document.getElementById('embarazo-field');
        const embarazo = document.getElementById('Embarazo');

        const updateSeguroVisibility = () => {
            const tieneSeguro = document.getElementById('tiene-seguro-si')?.checked === true;
            if (selector) selector.hidden = !tieneSeguro;
            if (seguro) {
                seguro.required = tieneSeguro;
                if (!tieneSeguro) seguro.value = '';
            }
        };

        radios.forEach(radio => radio.addEventListener('change', updateSeguroVisibility));
        updateSeguroVisibility();

        const updateEmbarazoVisibility = () => {
            const esFemenino = sexo?.value === 'Femenino';
            if (embarazoField) embarazoField.hidden = !esFemenino;
            if (embarazo && !esFemenino) embarazo.value = '';
        };

        sexo?.addEventListener('change', updateEmbarazoVisibility);
        updateEmbarazoVisibility();
    });
