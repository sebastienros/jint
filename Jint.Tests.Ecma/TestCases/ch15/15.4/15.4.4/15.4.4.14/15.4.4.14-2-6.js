/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-6.js
 * @description Array.prototype.indexOf - 'length' is an inherited data property
 */


function testcase() {
        var proto = { length: 2 };

        var Con = function () {};
        Con.prototype = proto;

        var childOne = new Con();
        childOne[1] = true;
        var childTwo = new Con();
        childTwo[2] = true;

        return Array.prototype.indexOf.call(childOne, true) === 1 &&
            Array.prototype.indexOf.call(childTwo, true) === -1;
    }
runTestCase(testcase);
