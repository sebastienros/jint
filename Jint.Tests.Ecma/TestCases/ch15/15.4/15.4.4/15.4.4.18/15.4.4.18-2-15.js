/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-2-15.js
 * @description Array.prototype.forEach - 'length' is property of the global object
 */


function testcase() {
        var result = false;
        function callbackfn(val, idx, obj) {
            result = (obj.length === 2);
        }

        try {
            var oldLen = fnGlobalObject().length;
            fnGlobalObject()[0] = 12;
            fnGlobalObject()[1] = 11;
            fnGlobalObject()[2] = 9;
            fnGlobalObject().length = 2;
            Array.prototype.forEach.call(fnGlobalObject(), callbackfn);
            return result;
        } finally {
            delete fnGlobalObject()[0];
            delete fnGlobalObject()[1];
            delete fnGlobalObject()[2];
            fnGlobalObject().length = oldLen;
        }
    }
runTestCase(testcase);
