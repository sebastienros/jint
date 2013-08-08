/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-37.js
 * @description Object.getOwnPropertyNames - inherited accessor properties are not pushed into the returned array
 */


function testcase() {
        var proto = {};
        Object.defineProperty(proto, "parent", {
            get: function () {
                return "parent";
            },
            configurable: true
        });

        var Con = function () { };
        Con.prototype = proto;

        var child = new Con();

        var result = Object.getOwnPropertyNames(child);

        for (var p in result) {
            if (result[p] === "parent") {
                return false;
            }
        }
        return true;
    }
runTestCase(testcase);
