/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-i-23.js
 * @description Array.prototype.indexOf - This object is the global object
 */


function testcase() {

        var targetObj = {};
        try {
            var oldLen = fnGlobalObject().length;
            fnGlobalObject()[0] = targetObj;
            fnGlobalObject()[100] = "100";
            fnGlobalObject()[200] = "200";
            fnGlobalObject().length = 200;
            return 0 === Array.prototype.indexOf.call(fnGlobalObject(), targetObj) &&
                100 === Array.prototype.indexOf.call(fnGlobalObject(), "100") &&
                -1 === Array.prototype.indexOf.call(fnGlobalObject(), "200");
        } finally {
            delete fnGlobalObject()[0];
            delete fnGlobalObject()[100];
            delete fnGlobalObject()[200];
            fnGlobalObject().length = oldLen;
        }
    }
runTestCase(testcase);
