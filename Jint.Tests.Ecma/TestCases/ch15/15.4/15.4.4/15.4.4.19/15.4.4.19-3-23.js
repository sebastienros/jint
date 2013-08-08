/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-3-23.js
 * @description Array.prototype.map uses inherited valueOf method when 'length' is an object with an own toString and inherited valueOf methods
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return val < 10;
        }

        var valueOfAccessed = false;
        var toStringAccessed = false;

        var proto = {
            valueOf: function () {
                valueOfAccessed = true;
                return 2;
            }
        };

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();

        child.toString = function () {
            toStringAccessed = true;
            return '1';
        };

        var obj = {
            0: 11,
            1: 9,
            length: child
        };

        var newArr = Array.prototype.map.call(obj, callbackfn);

        return newArr.length === 2 && valueOfAccessed && !toStringAccessed;
    }
runTestCase(testcase);
