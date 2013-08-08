/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-25.js
 * @description Array.prototype.lastIndexOf use inherited valueOf method when value of 'fromIndex' is an object with an own toString and inherited valueOf methods
 */


function testcase() {

        var toStringAccessed = false;
        var valueOfAccessed = false;

        var proto = {
            valueOf: function () {
                valueOfAccessed = true;
                return 1;
            }
        };

        var Con = function () {};
        Con.prototype = proto;

        var child = new Con();
        child.toString = function () {
            toStringAccessed = true;
            return 1;
        };

        return [0, true].lastIndexOf(true, child) === 1 && valueOfAccessed && !toStringAccessed;
    }
runTestCase(testcase);
