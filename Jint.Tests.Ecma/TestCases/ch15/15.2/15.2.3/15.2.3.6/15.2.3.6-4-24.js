/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-24.js
 * @description Object.defineProperty - 'name' is own data property that overrides an inherited data property (8.12.9 step 1)
 */


function testcase() {
        var proto = {};
        Object.defineProperty(proto, "foo", {
            value: 12,
            configurable: true
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;
        var obj = new ConstructFun();
        Object.defineProperty(obj, "foo", {
            value: 11,
            configurable: false
        });

        try {
            Object.defineProperty(obj, "foo", {
                configurable: true
            });
            return false;
        } catch (e) {
            return e instanceof TypeError && obj.foo === 11;
        }
    }
runTestCase(testcase);
