/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-4-a-3-s.js
 * @description Strict Mode - TypeError isn't thrown when deleting configurable data property
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var obj = {};
        Object.defineProperty(obj, "prop", {
            value: "abc",
            configurable: true
        });

        delete obj.prop;
        return !obj.hasOwnProperty("prop");
    }
runTestCase(testcase);
