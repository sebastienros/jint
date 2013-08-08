/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-9-c-ii-4-s.js
 * @description Array.prototype.reduceRight - undefined passed as thisValue to strict callbackfn
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
  [0].reduceRight(callbackfn,true);
  return innerThisCorrect;    
 }
runTestCase(testcase);
