/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-39.js
 * @description String.prototype.trim - 'this' is an object which has an own valueOf method
 */


function testcase() {
        var obj = {
            valueOf: function () {
                return "abc";
            }
        };

        return (String.prototype.trim.call(obj) === "[object Object]");
    }
runTestCase(testcase);
