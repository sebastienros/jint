/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-2-18.js
 * @description Array.prototype.map - applied to String object, which implements its own property get method
 */


function testcase() {
        function callbackfn(val, idx, obj) {
            return parseInt(val, 10) > 1;
        }

        var str = new String("432");
        try {
            String.prototype[3] = "1";
            var testResult = Array.prototype.map.call(str, callbackfn);

            return 3 === testResult.length;
        } finally {
            delete String.prototype[3];
        }
    }
runTestCase(testcase);
