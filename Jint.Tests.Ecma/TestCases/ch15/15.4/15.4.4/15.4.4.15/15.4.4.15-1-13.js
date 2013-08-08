/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-1-13.js
 * @description Array.prototype.lastIndexOf applied to the JSON object
 */


function testcase() {

        var targetObj = {};
        try {
            JSON[3] = targetObj;
            JSON.length = 5;
            return 3 === Array.prototype.lastIndexOf.call(JSON, targetObj);
        } finally {
            delete JSON[3];
            delete JSON.length;
        }
    }
runTestCase(testcase);
