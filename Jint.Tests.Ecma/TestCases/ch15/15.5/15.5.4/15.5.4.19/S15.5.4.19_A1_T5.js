// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * String.prototype.toLocaleUpperCase()
 *
 * @path ch15/15.5/15.5.4/15.5.4.19/S15.5.4.19_A1_T5.js
 * @description Call toLocaleUpperCase() function of function call
 */

//////////////////////////////////////////////////////////////////////////////
//CHECK#1
//since ToString(null) evaluates to "null" match(null) evaluates to match("null")
if (function(){return "GnulLuNa"}().toLocaleUpperCase() !== "GNULLUNA") {
  $ERROR('#1: function(){return "GnulLuNa"}().toLocaleUpperCase() === "GNULLUNA". Actual: '+function(){return "GnulLuNa"}().toLocaleUpperCase() );
}
//
//////////////////////////////////////////////////////////////////////////////

