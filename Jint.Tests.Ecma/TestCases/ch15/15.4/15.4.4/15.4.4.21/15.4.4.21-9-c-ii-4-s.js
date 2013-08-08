/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-9-c-ii-4-s.js
 * @description Array.prototype.reduce - undefined passed as thisValue to strict callbackfn
 * @onlyStrict
 */


function testcase() { 
  var innerThisCorrect = false;
  function callbackfn(prevVal, curVal, idx, obj)
  { 
     "use strict";
     innerThisCorrect = this===undefined;
     return true;
  }
  [0].reduce(callbackfn,true);
  return innerThisCorrect;    
 }
runTestCase(testcase);
