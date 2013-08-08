/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-7-c-i-23.js
 * @description Array.prototype.some - This object is an global object which contains index property
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            if (idx === 0) {
                return val === 11;
            }
            return false;
        }

        try {
            var oldLen = fnGlobalObject().length;
            fnGlobalObject()[0] = 11;
            fnGlobalObject().length = 1;
            return Array.prototype.some.call(fnGlobalObject(), callbackfn);
        } finally {
            delete fnGlobalObject()[0];
            fnGlobalObject().length = oldLen;
        }
    }
runTestCase(testcase);
