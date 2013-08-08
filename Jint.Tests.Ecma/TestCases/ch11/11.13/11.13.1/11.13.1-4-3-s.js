/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.1/11.13.1-4-3-s.js
 * @description simple assignment throws TypeError if LeftHandSide is a readonly property in strict mode (Global.Infinity)
 * @onlyStrict
 */


function testcase() {
    'use strict';

    try {
      fnGlobalObject().Infinity = 42;
      return false;
    }
    catch (e) {
      return (e instanceof TypeError);
    }
 }
runTestCase(testcase);
