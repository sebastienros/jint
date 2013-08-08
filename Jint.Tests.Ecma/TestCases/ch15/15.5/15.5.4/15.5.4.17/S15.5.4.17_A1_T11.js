// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * String.prototype.toLocaleLowerCase()
 *
 * @path ch15/15.5/15.5.4/15.5.4.17/S15.5.4.17_A1_T11.js
 * @description Override toString function, toString throw exception, then call toLocaleLowerCase() function for this object
 */

var __obj = {toString:function(){throw "intostr";}}
__obj.toLocaleLowerCase = String.prototype.toLocaleLowerCase;

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
try {
  var x = __obj.toLocaleLowerCase();
   	$FAIL('#1: "var x = __obj.toLocaleLowerCase()" lead to throwing exception');
} catch (e) {
  if (e!=="intostr") {
    $ERROR('#1.1: Exception === "intostr". Actual: '+e);
  }
}
//
//////////////////////////////////////////////////////////////////////////////

