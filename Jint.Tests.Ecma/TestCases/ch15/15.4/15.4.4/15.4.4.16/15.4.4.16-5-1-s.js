/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-1-s.js
 * @description Array.prototype.every - thisArg not passed to strict callbackfn
 * @onlyStrict
 */


function testcase() {
  var innerThisCorrect = false;

  function callbackfn(val, idx, obj) {
    "use strict";
    innerThisCorrect = this===undefined;
    return true;
  }

  [1].every(callbackfn);
  return innerThisCorrect;
 }
runTestCase(testcase);
