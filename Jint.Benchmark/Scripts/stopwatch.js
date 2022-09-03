function Stopwatch() {
    var sw = this;
    var start = null;
    var stop = null;
    var isRunning = false;

    sw.Start = function () {
        if (isRunning)
            return;

        start = new Date();
        stop = null;
        isRunning = true;
    }

    sw.Stop = function () {
        if (!isRunning)
            return;

        stop = new Date();
        isRunning = false;
    }

    sw.Reset = function () {
        start = isRunning ? new Date() : null;
        stop = null;
    }

    sw.Restart = function () {
        isRunning = true;
        sw.Reset();
    }

    sw.ElapsedMilliseconds = function () { return (isRunning ? new Date() : stop) - start; }
    sw.IsRunning = function() { return isRunning; }

}

var sw = new Stopwatch();
sw.Start();
for (var x = 0; x < 1021; x++) {
    for (var y = 0; y < 383; y++) {
        var z = x ^ y;
        if (z % 2 == 0)
            sw.Start();
        else if(z % 3 == 0)
            sw.Stop();
        else if (z % 5 == 0)
            sw.Reset();
        else if (z % 7 == 0)
            sw.Restart();
        var ms = sw.ElapsedMilliseconds;
        var rn = sw.IsRunning;
    }
}
sw.Stop();
