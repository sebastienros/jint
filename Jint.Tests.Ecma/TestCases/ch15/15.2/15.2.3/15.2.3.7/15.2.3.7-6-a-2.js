/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-2.js
 * @description Object.defineProperties - 'P' is inherited data property (8.12.9 step 1 ) 
 */


function testcase() {
        var proto = {};
        Object.defineProperty(proto, "prop", {
            value: 11,
            configurable: false
        });
        var Con = function () { };
        Con.prototype = proto;

        var obj = new Con();

        Object.defineProperties(obj, {
            prop: {
                value: 12,
                configurable: true
            }
        });

        return dataPropertyAttributesAreCorrect(obj, "prop", 12, false, false, true);

    }
runTestCase(testcase);
