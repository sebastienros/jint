/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch08/8.7/8.7.2/8.7.2-2-s.js
 * @description Strict Mode - ReferenceError isn't thrown if LeftHandSide evaluates to a resolvable Reference
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var b = 11;
        return b === 11;
    }
runTestCase(testcase);
