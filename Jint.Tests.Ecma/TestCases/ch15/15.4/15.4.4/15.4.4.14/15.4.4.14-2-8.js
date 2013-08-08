/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-2-8.js
 * @description Array.prototype.indexOf - 'length' is own accessor property that overrides an inherited data property
 */


function testcase() {

        var proto = { length: 0 };

        var Con = function () {};
        Con.prototype = proto;

        var child = new Con();
        child[1] = true;

        Object.defineProperty(child, "length", {
            get: function () {
                return 2;
            },
            configurable: true
        });

        return Array.prototype.indexOf.call(child, true) === 1;
    }
runTestCase(testcase);
