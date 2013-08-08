/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-25.js
 * @description Array.prototype.indexOf uses inherited valueOf method when value of 'fromIndex' is an object with an own toString and inherited valueOf methods
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
            return 2;
        };

        return [0, true].indexOf(true, child) === 1 && valueOfAccessed && !toStringAccessed;
    }
runTestCase(testcase);
