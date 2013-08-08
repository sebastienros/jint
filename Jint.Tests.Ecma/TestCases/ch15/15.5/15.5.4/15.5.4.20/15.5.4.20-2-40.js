/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-40.js
 * @description String.prototype.trim - 'this' is an object that has an own toString method that returns an object and valueOf method that returns a primitive value
 */


function testcase() {
        var toStringAccessed = false;
        var valueOfAccessed = false;
        var obj = {
            toString: function () {
                toStringAccessed = true;
                return {};
            },
            valueOf: function () {
                valueOfAccessed = true;
                return "abc";
            }
        };
        return (String.prototype.trim.call(obj) === "abc") && valueOfAccessed && toStringAccessed;
    }
runTestCase(testcase);
