/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-3-3.js
 * @description Object.defineProperties - enumerable inherited data property of 'Properties' is not defined in 'O' 
 */


function testcase() {

        var obj = {};

        var proto = {};

        Object.defineProperty(proto, "prop", {
            value: {},
            enumerable: true
        });

        var Con = function () { };
        Con.prototype = proto;
        var child = new Con();

        Object.defineProperties(obj, child);

        return !obj.hasOwnProperty("prop");
    }
runTestCase(testcase);
