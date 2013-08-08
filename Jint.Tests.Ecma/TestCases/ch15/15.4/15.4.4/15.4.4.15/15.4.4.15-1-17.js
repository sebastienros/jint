/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-17.js
 * @description Array.prototype.lastIndexOf applied to the global object
 */


function testcase() {
        var targetObj = ["global"];
        try {
            var oldLen = fnGlobalObject().length;
            fnGlobalObject()[1] = targetObj;
            fnGlobalObject().length = 3;
            return 1 === Array.prototype.lastIndexOf.call(fnGlobalObject(), targetObj);
        } finally {
            delete fnGlobalObject()[1];
            fnGlobalObject().length = oldLen;
        }
    }
runTestCase(testcase);
