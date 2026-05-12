function decodePlayerjsFile(input) {
    const ctx = {
        dm: null,
        _ml: null,

        ex: [
            "slice",
            "atob",
            "substr",
            "salt",
            "temp",
            "escape",
            "push",
            "this",
            "btoa",
            "pop",
            "JSON.stringify",
            "length",
            "JSON.parse",
            "forEach",
            "splice",
            "decodeURIComponent",
            "unshift",
            "",
            "clone",
            "insert"
        ],

        load(e) {
            if (typeof e === "object") return e;

            if (typeof e === "string" && e.indexOf("#2") === 0) {
                return this.loadFromString(e.substr(2));
            }

            return "";
        },

        loadFromString(e) {
            this.dm = e.substr(0, 2);
            this._ml = Math.pow(2, 5);

            return this.readString(
                this.slice(e.substr(2)),
                1, 5, 15, 12
            );
        },

        slice(e) {
            return e
                .split(String.fromCharCode(this.dm))
                .map((part) => {
                    const t = parseInt(part.slice(-1), 10);

                    return part.length > this._ml
                        ? part.substr(2 * t, part.length - 3 * t - 1) + part.substr(0, t)
                        : part;
                })
                .join(this.ex[17]);
        },

        readString(val, ...actions) {
            const padding = val.length % 4;

            if (padding) {
                val += "=".repeat(4 - padding);
            }

            actions.forEach((v) => {
                switch (this.ex[v]) {
                    case "atob":
                        val = atob(val);
                        break;

                    case "escape":
                        val = escape(val);
                        break;

                    case "decodeURIComponent":
                        val = decodeURIComponent(val);
                        break;

                    case "JSON.parse":
                        val = JSON.parse(val);
                        break;

                    default:
                        throw new Error("Unsupported action: " + this.ex[v]);
                }
            });

            return val;
        }
    };

    return JSON.stringify(ctx.load(input));
}
