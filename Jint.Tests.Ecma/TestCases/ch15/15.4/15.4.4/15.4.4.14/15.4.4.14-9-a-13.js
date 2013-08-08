/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-a-13.js
 * @description Array.prototype.indexOf - deleting property of prototype causes prototype index property not to be visited on an Array-like Object
 */


function testcase() {

        var arr = { 2: 2, length: 20 };

        Object.defineProperty(arr, "0", {
            get: function () {
                delete Object.prototype[1];
                return 0;
            },
            configurable: true
        });

        try {
            Object.prototype[1] = 1;
            return -1 === Array.prototype.indexOf.call(arr, 1);
        } finally {
            delete Object.prototype[1];
        }
    }
runTestCase(testcase);
