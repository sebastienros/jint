/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-41.js
 * @description String.prototype.trim - 'this' is an object which has an own toString and valueOf method.
 */


function testcase() {
        var toStringAccessed = false;
        var valueOfAccessed = false;
        var obj = {
            toString: function () {
                toStringAccessed = true;
                return "abc";
            },
            valueOf: function () {
                valueOfAccessed = true;
                return "cef";
            }
        };
        return (String.prototype.trim.call(obj) === "abc") && !valueOfAccessed && toStringAccessed;
    }
runTestCase(testcase);
