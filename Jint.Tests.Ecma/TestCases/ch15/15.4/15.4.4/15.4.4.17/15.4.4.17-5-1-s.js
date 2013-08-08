/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-5-1-s.js
 * @description Array.prototype.some - thisArg not passed to strict callbackfn
 * @onlyStrict
 */


function testcase() {
  var innerThisCorrect = false;

  function callbackfn(val, idx, obj) {
    "use strict";
    innerThisCorrect = this===undefined;
    return true;
  }

  [1].some(callbackfn);
  return innerThisCorrect;
 }
runTestCase(testcase);
