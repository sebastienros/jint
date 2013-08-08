/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-3-23.js
 * @description Array.prototype.indexOf uses inherited valueOf method when 'length' is an object with an own toString and inherited valueOf methods
 */


function testcase() {

        var toStringAccessed = false;
        var valueOfAccessed = false;

        var proto = {
            valueOf: function () {
                valueOfAccessed = true;
                return 2;
            }
        };

        var Con = function () {};
        Con.prototype = proto;

        var child = new Con();
        child.toString = function () {
            toStringAccessed = true;
            return 2;
        };

        var obj = {
            1: true,
            length: child
        };

        return Array.prototype.indexOf.call(obj, true) === 1 && valueOfAccessed && !toStringAccessed;
    }
runTestCase(testcase);
