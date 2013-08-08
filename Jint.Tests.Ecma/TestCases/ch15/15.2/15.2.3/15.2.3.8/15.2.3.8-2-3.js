/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.8/15.2.3.8-2-3.js
 * @description Object.seal - inherited accessor properties are ignored
 */


function testcase() {
        var proto = {};

        Object.defineProperty(proto, "Father", {
            get: function () {
                return 10;
            },
            configurable: true
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var child = new ConstructFun();
        var preCheck = Object.isExtensible(child);
        Object.seal(child);

        var beforeDeleted = proto.hasOwnProperty("Father");
        delete proto.Father;
        var afterDeleted = proto.hasOwnProperty("Father");

        return preCheck && beforeDeleted && !afterDeleted;
    }
runTestCase(testcase);
